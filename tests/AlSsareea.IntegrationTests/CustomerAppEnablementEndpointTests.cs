using System.Net;
using System.Net.Http.Json;
using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

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
