using AlSsareea.Modules.Customers.Application;
using AlSsareea.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Customers.Infrastructure.Persistence;

internal sealed class CustomerRepository(CustomersDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(CustomerId id, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Customer> query = Aggregate(includeDeleted);
        return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Customer?> GetByUserIdAsync(Guid userId, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Customer> query = Aggregate(includeDeleted);
        return query.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
        => db.Customers.AddAsync(customer, cancellationToken).AsTask();

    private IQueryable<Customer> Aggregate(bool includeDeleted)
    {
        IQueryable<Customer> query = db.Customers;
        if (includeDeleted) query = query.IgnoreQueryFilters();
        return query.Include(x => x.Addresses).Include(x => x.Preferences).Include(x => x.StatusHistory);
    }
}
