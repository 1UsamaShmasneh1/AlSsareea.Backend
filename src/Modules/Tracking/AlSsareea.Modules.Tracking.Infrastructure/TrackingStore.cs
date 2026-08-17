using AlSsareea.Modules.Tracking.Application;
using AlSsareea.Modules.Tracking.Contracts;
using AlSsareea.Modules.Tracking.Domain;
using AlSsareea.Modules.Tracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;

namespace AlSsareea.Modules.Tracking.Infrastructure;

internal sealed class TrackingStore(TrackingDbContext db) : ITrackingStore
{
    public Task<DriverLatestLocation?> GetLatestEntityAsync(Guid driverId, CancellationToken ct) => db.DriverLatestLocations.AsNoTracking().SingleOrDefaultAsync(x => x.DriverId == driverId, ct);
    public async Task<StoreLocationResult> StoreAsync(DriverLocation location, bool promoteLatest, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.DriverLocations.Add(location); await db.SaveChangesAsync(ct); bool updated = false;
            if (promoteLatest)
            {
                Point point = new(location.Position.Longitude, location.Position.Latitude) { SRID = 4326 };
                int affected = await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO tracking.driver_latest_locations
                    (driver_id, location_id, position, recorded_at_utc, received_at_utc, accuracy_meters, speed_meters_per_second, heading_degrees, last_sequence_number, updated_at_utc, concurrency_stamp)
                    VALUES ({location.DriverId}, {location.Id.Value}, {point}, {location.RecordedAtUtc}, {location.ReceivedAtUtc}, {location.AccuracyMeters}, {location.SpeedMetersPerSecond}, {location.HeadingDegrees}, {location.SequenceNumber}, {location.ReceivedAtUtc}, {Guid.NewGuid()})
                    ON CONFLICT (driver_id) DO UPDATE SET location_id = EXCLUDED.location_id, position = EXCLUDED.position, recorded_at_utc = EXCLUDED.recorded_at_utc,
                    received_at_utc = EXCLUDED.received_at_utc, accuracy_meters = EXCLUDED.accuracy_meters, speed_meters_per_second = EXCLUDED.speed_meters_per_second,
                    heading_degrees = EXCLUDED.heading_degrees, last_sequence_number = EXCLUDED.last_sequence_number, updated_at_utc = EXCLUDED.updated_at_utc, concurrency_stamp = EXCLUDED.concurrency_stamp
                    WHERE EXCLUDED.last_sequence_number > tracking.driver_latest_locations.last_sequence_number
                       OR (EXCLUDED.last_sequence_number = tracking.driver_latest_locations.last_sequence_number AND EXCLUDED.recorded_at_utc > tracking.driver_latest_locations.recorded_at_utc)", ct);
                updated = affected > 0;
            }
            await transaction.CommitAsync(ct); return new(false, updated, location.Id);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            DriverLocationId existingLocationId = await db.DriverLocations
                .AsNoTracking()
                .Where(x => x.DriverId == location.DriverId && x.SequenceNumber == location.SequenceNumber)
                .Select(x => x.Id)
                .SingleAsync(ct);
            return new(true, false, existingLocationId);
        }
    }
    public async Task<DriverLocationResponse?> GetLatestAsync(Guid driverId, CancellationToken ct)
    {
        DriverLatestLocation? value = await db.DriverLatestLocations.AsNoTracking().SingleOrDefaultAsync(x => x.DriverId == driverId, ct); return value is null ? null : Map(value);
    }
    public async Task<DriverLocationHistoryResponse> GetHistoryAsync(Guid driverId, DateTime fromUtc, DateTime toUtc, int page, int pageSize, CancellationToken ct)
    {
        IQueryable<DriverLocation> query = db.DriverLocations.AsNoTracking().Where(x => x.DriverId == driverId && x.RecordedAtUtc >= fromUtc && x.RecordedAtUtc <= toUtc); int total = await query.CountAsync(ct);
        DriverLocation[] items = await query.OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.SequenceNumber).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(items.Select(Map).ToArray(), page, pageSize, total);
    }
    private static DriverLocationResponse Map(DriverLocation value) => new(value.Id.Value, value.DriverId, value.Position.Latitude, value.Position.Longitude, value.RecordedAtUtc, value.ReceivedAtUtc, value.AccuracyMeters, value.SpeedMetersPerSecond, value.HeadingDegrees, value.SequenceNumber);
    private static DriverLocationResponse Map(DriverLatestLocation value) => new(value.LocationId.Value, value.DriverId, value.Position.Latitude, value.Position.Longitude, value.RecordedAtUtc, value.ReceivedAtUtc, value.AccuracyMeters, value.SpeedMetersPerSecond, value.HeadingDegrees, value.LastSequenceNumber);
}

internal sealed class NullLocationRealtimePublisher : ILocationRealtimePublisher { public Task PublishAsync(Guid driverId, TrackingRealtimePayload payload, CancellationToken cancellationToken) => Task.CompletedTask; }
internal sealed class UnavailableTrackingVisibilityProvider : ITrackingVisibilityProvider { public Task<TrackingVisibility?> ResolveOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<TrackingVisibility?>(null); }
