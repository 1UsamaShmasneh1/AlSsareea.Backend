using AlSsareea.Modules.Catalog.Application;
using AlSsareea.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Catalog.Infrastructure.Persistence;

internal sealed class CatalogRepository(CatalogDbContext db) : ICatalogRepository
{
    public Task<Catalog.Domain.Catalog?> GetAsync(Guid merchantId, CancellationToken ct = default) => db.Catalogs.SingleOrDefaultAsync(x => x.MerchantId == merchantId, ct);
    public async Task AddAsync(Catalog.Domain.Catalog catalog, CancellationToken ct = default) => await db.Catalogs.AddAsync(catalog, ct);
}
internal sealed class ProductRepository(CatalogDbContext db) : IProductRepository
{
    public Task<Product?> GetAsync(Guid merchantId, ProductId id, bool tracked = true, CancellationToken ct = default)
    {
        IQueryable<Product> query = db.Products.Include(x => x.Translations).Include(x => x.Variants).ThenInclude(x => x.Translations).Include(x => x.OptionGroups).ThenInclude(x => x.Translations).Include(x => x.OptionGroups).ThenInclude(x => x.Options).ThenInclude(x => x.Translations).Include(x => x.Images).Include(x => x.Availability);
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.MerchantId == merchantId && x.Id == id, ct);
    }
    public async Task AddAsync(Product product, CancellationToken ct = default) => await db.Products.AddAsync(product, ct);
}
