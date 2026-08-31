using System.Net;
using System.Net.Http.Json;
using AlSsareea.Modules.Carts.Contracts;
using AlSsareea.Modules.Catalog.Contracts;
using AlSsareea.Modules.Catalog.Infrastructure.Persistence;
using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using CatalogAggregate = AlSsareea.Modules.Catalog.Domain.Catalog;
using CatalogId = AlSsareea.Modules.Catalog.Domain.CatalogId;
using InventoryStatus = AlSsareea.Modules.Catalog.Domain.InventoryStatus;
using Product = AlSsareea.Modules.Catalog.Domain.Product;
using ProductId = AlSsareea.Modules.Catalog.Domain.ProductId;
using SelectionType = AlSsareea.Modules.Catalog.Domain.SelectionType;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class CustomerAppEnablementEndpointTests(PostgresFixture fixture)
{
    [Fact]
    public async Task PublicDiscoveryReturnsOnlyActiveMerchantWithVisibleBranchAndNoInternalFields()
    {
        Guid activeId = await SeedMerchant(true, "Customer Visible");
        _ = await SeedMerchant(false, "Internal Pending");
        HttpClient client = fixture.ApiFactory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/customer/merchants/?page=1&pageSize=10&query=Customer");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync();
        CustomerMerchantListResponse body = (await response.Content.ReadFromJsonAsync<CustomerMerchantListResponse>())!;
        Assert.Equal(activeId, Assert.Single(body.Items).Id);
        Assert.DoesNotContain("ownerUserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concurrencyStamp", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicDetailsConcealsInactiveMerchant()
    {
        Guid merchantId = await SeedMerchant(false, "Pending");
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.ApiFactory.CreateClient().GetAsync($"/api/v1/customer/merchants/{merchantId}")).StatusCode);
    }

    [Fact]
    public async Task MapsRequiresAuthenticationAndUsesProviderNeutralResponses()
    {
        HttpClient client = fixture.ApiFactory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/maps/geocode", new GeocodingRequest("Ramallah"))).StatusCode);
    }

    [Fact]
    public async Task PublicProductDetailsExposeOrderedCustomerConfigurationAndCartStableIds()
    {
        Guid merchantId = await SeedMerchant(true, "Configured Product Merchant");
        await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            CatalogDbContext db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            DateTime now = DateTime.UtcNow;
            CatalogAggregate catalog = CatalogAggregate.Create(CatalogId.New(), merchantId, "Customer Catalog", null, "en", now);
            Product product = Product.Create(ProductId.New(), catalog.Id, merchantId, null, "CUSTOMER-MEAL", 1200, "ILS", "food", 0, now);
            product.SetTranslation("en", "Customer Meal", "Customer-safe description", now.AddSeconds(1));
            var unavailableVariant = product.AddVariant("en", "Large", null, 300, InventoryStatus.OutOfStock, false, 2, now.AddSeconds(2));
            var availableVariant = product.AddVariant("en", "Regular", null, 0, InventoryStatus.InStock, true, 1, now.AddSeconds(3));
            var required = product.AddOptionGroup("en", "Size", SelectionType.SingleChoice, true, 1, 1, 1, now.AddSeconds(4));
            var requiredOption = required.AddOption("en", "Standard", 0, true, true, 1, now.AddSeconds(5));
            var optional = product.AddOptionGroup("en", "Extras", SelectionType.MultipleChoice, false, 0, 2, 2, now.AddSeconds(6));
            _ = optional.AddOption("en", "Unavailable cheese", 100, false, false, 2, now.AddSeconds(7));
            _ = optional.AddOption("en", "Olives", 50, false, true, 1, now.AddSeconds(8));
            product.AddImage(null, "https://cdn.example.test/product.webp", "Customer Meal", 0, true, now.AddSeconds(9));
            product.Publish("en", now.AddSeconds(10));
            catalog.Activate(true, now.AddSeconds(11));
            db.AddRange(catalog, product);
            await db.SaveChangesAsync();

            HttpResponseMessage response = await fixture.ApiFactory.CreateClient().GetAsync($"/api/v1/merchants/{merchantId}/catalog/products/{product.Id.Value}?language=en");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            CustomerProductDetailsResponse body = (await response.Content.ReadFromJsonAsync<CustomerProductDetailsResponse>())!;
            Assert.True(body.IsAvailable);
            Assert.Equal(product.Id.Value, body.Id);
            Assert.Equal([availableVariant.Id.Value, unavailableVariant.Id.Value], body.Variants.Select(value => value.Id));
            Assert.True(body.Variants[0].IsAvailable);
            Assert.False(body.Variants[1].IsAvailable);
            Assert.Equal([required.Id.Value, optional.Id.Value], body.OptionGroups.Select(value => value.Id));
            Assert.True(body.OptionGroups[0].IsRequired);
            Assert.Equal(1, body.OptionGroups[0].MinSelections);
            Assert.Equal(1, body.OptionGroups[0].MaxSelections);
            Assert.Equal(requiredOption.Id.Value, Assert.Single(body.OptionGroups[0].Options).Id);
            Assert.False(body.OptionGroups[1].Options[1].IsAvailable);
            Assert.Equal("https://cdn.example.test/product.webp", Assert.Single(body.Media).Url);
            Assert.Equal(1200, body.BasePriceMinor);
            var cartRequest = new AddCartItemRequest(body.Id, body.Variants[0].Id, 1, null, [new(body.OptionGroups[0].Id, body.OptionGroups[0].Options[0].Id)], Guid.NewGuid());
            Assert.Equal(required.Id.Value, Assert.Single(cartRequest.SelectedOptions).OptionGroupId);
            HttpResponseMessage priceResponse = await fixture.ApiFactory.CreateClient().PostAsJsonAsync($"/api/v1/merchants/{merchantId}/catalog/products/{body.Id}/price", new PriceRequest(cartRequest.ProductVariantId, cartRequest.SelectedOptions.Select(value => value.OptionItemId).ToArray(), "en"));
            Assert.Equal(HttpStatusCode.OK, priceResponse.StatusCode);
            CatalogPriceResponse quote = (await priceResponse.Content.ReadFromJsonAsync<CatalogPriceResponse>())!;
            Assert.Equal(availableVariant.Id.Value, quote.SelectedVariant!.Id);
            Assert.Equal(requiredOption.Id.Value, Assert.Single(quote.SelectedOptions).Id);
            string json = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("storageKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ownerType", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<Guid> SeedMerchant(bool active, string displayName)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        MerchantsDbContext db = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        DateTime now = DateTime.UtcNow;
        Merchant merchant = Merchant.Create(MerchantId.New(), displayName + " Legal", displayName, "Public description", null, null, "merchant@example.com", "+970599000000", Guid.NewGuid(), now);
        MerchantBranch branch = MerchantBranch.Create(MerchantBranchId.New(), merchant.Id, "Main", null, "+970599000001", null, BranchAddress.Create("Ramallah", null, "Main Street", null, null), new GeoCoordinate(31.9, 35.2), "Asia/Jerusalem", true, now);
        if (active) { merchant.Activate(now); branch.Activate(true, now); }
        db.AddRange(merchant, branch);
        await db.SaveChangesAsync();
        return merchant.Id.Value;
    }
}
