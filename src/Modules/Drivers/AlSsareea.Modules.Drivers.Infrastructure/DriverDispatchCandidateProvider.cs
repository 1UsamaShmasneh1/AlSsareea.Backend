using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Drivers.Domain;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Drivers.Infrastructure;

internal sealed class DriverDispatchCandidateProvider(DriversDbContext db, IClock clock) : IDriverDispatchCandidateProvider
{
    public async Task<IReadOnlyList<DriverDispatchCandidateSnapshot>> FindAsync(Guid zoneId, short? requiredVehicleType, int maximumResults, CancellationToken ct = default)
    {
        IQueryable<Driver> query = db.Drivers.AsNoTracking().Include(x => x.Vehicles).Include(x => x.ZoneAssignments).Include(x => x.Suspensions);
        if (zoneId != Guid.Empty) query = query.Where(x => x.ZoneAssignments.Any(z => z.ZoneId == zoneId && z.IsActive));
        Driver[] values = await query.OrderBy(x => x.CurrentLoad).ThenBy(x => x.LastAvailabilityChangedAtUtc).ThenBy(x => x.Id).Take(Math.Clamp(maximumResults * 3, 1, 300)).AsSplitQuery().ToArrayAsync(ct);
        DateTime now = clock.UtcNow;
        return values.Select(driver =>
        {
            Vehicle? vehicle = driver.Vehicles.FirstOrDefault(x => x.IsPrimary && x.Status == VehicleStatus.Active);
            return new DriverDispatchCandidateSnapshot(driver.Id.Value, driver.IsOperationallyActiveAt(now), driver.ActivationStatus == DriverActivationStatus.Approved, (short)driver.AvailabilityStatus, vehicle is null ? null : (short)vehicle.Type, driver.ZoneAssignments.Where(x => x.IsActive).Select(x => x.ZoneId).ToArray(), driver.MaximumConcurrentDeliveries, driver.CurrentLoad, driver.HasActiveSuspension(now), null);
        }).Where(x => x.IsActive && x.IsApproved && !x.HasActiveSuspension && x.AvailabilityStatus is (short)AvailabilityStatus.Online or (short)AvailabilityStatus.Busy && x.CurrentLoad < x.MaximumCapacity && (!requiredVehicleType.HasValue || x.PrimaryVehicleType == requiredVehicleType)).Take(Math.Clamp(maximumResults, 1, 100)).ToArray();
    }
}
