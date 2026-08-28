using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Customers.Domain;
using AlSsareea.Modules.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Customers.Infrastructure;

internal sealed class CustomerIdentityProvider(CustomersDbContext db) : ICustomerIdentityProvider
{
    public async Task<Guid?> GetUserIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty) return null;
        CustomerId id = new(customerId);
        return await db.Customers.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(cancellationToken);
    }
}
