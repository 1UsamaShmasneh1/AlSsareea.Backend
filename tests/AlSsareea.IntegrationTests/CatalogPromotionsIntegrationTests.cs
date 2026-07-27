using AlSsareea.Modules.Catalog.Contracts;
using AlSsareea.Modules.Catalog.Domain;
using AlSsareea.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using CatalogAggregate = AlSsareea.Modules.Catalog.Domain.Catalog;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class CatalogPromotionsIntegrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task PromotionScopeContractValidatesStrongCatalogIdsWithinMerchantBoundary()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        CatalogDbContext db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        ICatalogPromotionScopeProvider provider = scope.ServiceProvider.GetRequiredService<ICatalogPromotionScopeProvider>();
        DateTime now = DateTime.UtcNow;
        Guid merchantId = Guid.NewGuid();
        Guid otherMerchantId = Guid.NewGuid();
        CatalogAggregate catalog = CatalogAggregate.Create(CatalogId.New(), merchantId, "Promotions catalog", null, "en", now);
        Category category = Category.Create(CategoryId.New(), catalog.Id, merchantId, null, 0, "en", "Eligible", null, now);
        Product product = Product.Create(ProductId.New(), catalog.Id, merchantId, category.Id, null, 1000, "ILS", null, 0, now);
        product.SetTranslation("en", "Eligible product", null, now);
        db.AddRange(catalog, category, product);
        await db.SaveChangesAsync();

        Assert.True(await provider.ProductsBelongToMerchantAsync(merchantId, [product.Id.Value]));
        Assert.True(await provider.CategoriesBelongToMerchantAsync(merchantId, [category.Id.Value]));
        Assert.False(await provider.ProductsBelongToMerchantAsync(otherMerchantId, [product.Id.Value]));
        Assert.False(await provider.CategoriesBelongToMerchantAsync(otherMerchantId, [category.Id.Value]));
    }
}
