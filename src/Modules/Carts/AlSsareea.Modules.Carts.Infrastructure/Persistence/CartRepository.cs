using AlSsareea.Modules.Carts.Application;
using AlSsareea.Modules.Carts.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Carts.Infrastructure.Persistence;

internal sealed class CartRepository(CartsDbContext db) : ICartRepository
{
    private IQueryable<Cart> Query() => db.Carts.Include(x => x.Items).ThenInclude(x => x.SelectedOptions);
    public Task<Cart?> GetAsync(CartId id, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Cart> query = Query(); if (!tracking) query = query.AsNoTracking(); return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
    public Task<Cart?> GetActiveAsync(Guid customerId, Guid merchantId, Guid? branchId, CancellationToken cancellationToken) =>
        Query().SingleOrDefaultAsync(x => x.CustomerId == customerId && x.MerchantId == merchantId && x.BranchId == branchId && x.Status == CartStatus.Active, cancellationToken);
    public Task AddAsync(Cart cart, CancellationToken cancellationToken) => db.Carts.AddAsync(cart, cancellationToken).AsTask();
}
