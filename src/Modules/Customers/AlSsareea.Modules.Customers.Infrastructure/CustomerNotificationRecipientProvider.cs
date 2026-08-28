using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Customers.Domain;
using AlSsareea.Modules.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Customers.Infrastructure;

internal sealed class CustomerNotificationRecipientProvider(CustomersDbContext db) : ICustomerNotificationRecipientProvider
{
    public Task<CustomerNotificationRecipient?> GetAsync(Guid customerId, CancellationToken ct = default) => db.Customers.AsNoTracking().Where(x => x.Id == new CustomerId(customerId)).Select(x => new CustomerNotificationRecipient(x.UserId, x.Preferences.PreferredLanguage)).SingleOrDefaultAsync(ct);
}
