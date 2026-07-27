using AlSsareea.Modules.Catalog.Contracts;
using AlSsareea.Modules.Catalog.Domain;
using AlSsareea.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Catalog.Infrastructure;

internal sealed class CatalogPromotionScopeProvider(CatalogDbContext db) : ICatalogPromotionScopeProvider
{
    public async Task<bool> ProductsBelongToMerchantAsync(
        Guid merchantId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (merchantId == Guid.Empty || productIds.Count == 0 || productIds.Any(x => x == Guid.Empty))
            return false;

        ProductId[] ids = productIds.Distinct().Select(x => new ProductId(x)).ToArray();
        int count = await db.Products.AsNoTracking()
            .CountAsync(x => x.MerchantId == merchantId && ids.Contains(x.Id), cancellationToken);
        return count == ids.Length;
    }

    public async Task<bool> CategoriesBelongToMerchantAsync(
        Guid merchantId,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default)
    {
        if (merchantId == Guid.Empty || categoryIds.Count == 0 || categoryIds.Any(x => x == Guid.Empty))
            return false;

        CategoryId[] ids = categoryIds.Distinct().Select(x => new CategoryId(x)).ToArray();
        int count = await db.Categories.AsNoTracking()
            .CountAsync(x => x.MerchantId == merchantId && ids.Contains(x.Id), cancellationToken);
        return count == ids.Length;
    }
}
