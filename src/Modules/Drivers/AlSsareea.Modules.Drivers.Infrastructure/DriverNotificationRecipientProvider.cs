using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Drivers.Domain;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Drivers.Infrastructure;

internal sealed class DriverNotificationRecipientProvider(DriversDbContext db) : IDriverNotificationRecipientProvider
{
    public Task<DriverNotificationRecipient?> GetAsync(Guid driverId, CancellationToken ct = default) => db.Drivers.AsNoTracking().Where(x => x.Id == new DriverId(driverId)).Select(x => new DriverNotificationRecipient(x.UserId, "ar")).SingleOrDefaultAsync(ct);
}
