using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Tracking.Contracts;
using AlSsareea.Modules.Tracking.Domain;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Tracking.Application;

public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";
    public int FutureToleranceSeconds { get; init; } = 30;
    public int MaximumLiveStalenessSeconds { get; init; } = 300;
    public int OfflineSyncWindowHours { get; init; } = 24;
    public int MaximumBatchSize { get; init; } = 200;
    public int IngestionPermitLimit { get; init; } = 240;
    public double MaximumAccuracyMeters { get; init; } = 250;
    public double MaximumPlausibleSpeedMetersPerSecond { get; init; } = 75;
    public int OfflineIntervalSeconds { get; init; } = 120;
    public int IdleIntervalSeconds { get; init; } = 30;
    public int BusyIntervalSeconds { get; init; } = 10;
    public int LocationHistoryRetentionDays { get; init; } = 30;
    public int MaximumHistoryRangeHours { get; init; } = 24;
    public int MaximumHistoryPageSize { get; init; } = 100;
}

public enum TrackingStatus { Accepted, HistoryOnly, Duplicate, Rejected, NotFound, Forbidden }
public sealed record TrackingResult<T>(TrackingStatus Status, T? Value = default, string? ErrorCode = null);
public sealed record TrackingActor(Guid UserId);
public sealed record StoreLocationResult(bool Duplicate, bool LatestUpdated, DriverLocationId? LocationId = null);
public interface ITrackingStore
{
    Task<DriverLatestLocation?> GetLatestEntityAsync(Guid driverId, CancellationToken cancellationToken);
    Task<StoreLocationResult> StoreAsync(DriverLocation location, bool promoteLatest, CancellationToken cancellationToken);
    Task<DriverLocationResponse?> GetLatestAsync(Guid driverId, CancellationToken cancellationToken);
    Task<DriverLocationHistoryResponse> GetHistoryAsync(Guid driverId, DateTime fromUtc, DateTime toUtc, int page, int pageSize, CancellationToken cancellationToken);
}
public interface ILocationRealtimePublisher { Task PublishAsync(Guid driverId, TrackingRealtimePayload payload, CancellationToken cancellationToken); }
public interface ITrackingService
{
    Task<TrackingResult<LocationUpdateResponse>> UpdateAsync(TrackingActor actor, LocationUpdateRequest request, CancellationToken cancellationToken);
    Task<TrackingResult<LocationBatchResponse>> BatchAsync(TrackingActor actor, LocationBatchRequest request, CancellationToken cancellationToken);
    Task<TrackingResult<DriverLocationResponse>> GetMineAsync(TrackingActor actor, CancellationToken cancellationToken);
    Task<TrackingResult<DriverLocationResponse>> GetLatestAsync(Guid driverId, CancellationToken cancellationToken);
    Task<TrackingResult<DriverLocationHistoryResponse>> GetHistoryAsync(Guid driverId, DateTime fromUtc, DateTime toUtc, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class TrackingService(ITrackingStore store, IDriverOperationalSnapshotProvider drivers, IClock clock, ILocationRealtimePublisher realtime, IOptions<TrackingOptions> options) : ITrackingService
{
    private readonly TrackingOptions settings = options.Value;
    public async Task<TrackingResult<LocationUpdateResponse>> UpdateAsync(TrackingActor actor, LocationUpdateRequest request, CancellationToken cancellationToken)
    {
        DriverEligibilitySnapshot? driver = await Resolve(actor, cancellationToken); if (driver is null) return Failure<LocationUpdateResponse>(TrackingStatus.Forbidden, "tracking.driver_not_eligible");
        return await Accept(driver, request, LocationSource.Live, null, cancellationToken);
    }
    public async Task<TrackingResult<LocationBatchResponse>> BatchAsync(TrackingActor actor, LocationBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.BatchId == Guid.Empty || request.Locations.Count is 0 || request.Locations.Count > settings.MaximumBatchSize) return Failure<LocationBatchResponse>(TrackingStatus.Rejected, "tracking.invalid_batch");
        DriverEligibilitySnapshot? driver = await Resolve(actor, cancellationToken); if (driver is null) return Failure<LocationBatchResponse>(TrackingStatus.Forbidden, "tracking.driver_not_eligible");
        var results = new List<LocationUpdateResponse>(request.Locations.Count); int accepted = 0, history = 0, duplicates = 0, rejected = 0;
        foreach (LocationUpdateRequest item in request.Locations.OrderBy(x => x.SequenceNumber).ThenBy(x => x.RecordedAtUtc))
        {
            TrackingResult<LocationUpdateResponse> result = await Accept(driver, item, LocationSource.OfflineBatch, new LocationBatchId(request.BatchId), cancellationToken);
            if (result.Value is { } value) { results.Add(value); if (value.Status == "accepted") accepted++; else if (value.Status == "history-only") history++; else if (value.Status == "duplicate") duplicates++; }
            else { rejected++; results.Add(new(Guid.Empty, item.SequenceNumber, "rejected", false, Interval(driver.AvailabilityStatus))); }
        }
        return new(TrackingStatus.Accepted, new(request.BatchId, accepted, history, duplicates, rejected, results));
    }
    public async Task<TrackingResult<DriverLocationResponse>> GetMineAsync(TrackingActor actor, CancellationToken cancellationToken) { DriverEligibilitySnapshot? driver = await drivers.GetByUserAsync(actor.UserId, cancellationToken); return driver is null ? Failure<DriverLocationResponse>(TrackingStatus.NotFound, "tracking.driver_not_found") : await GetLatestAsync(driver.DriverId, cancellationToken); }
    public async Task<TrackingResult<DriverLocationResponse>> GetLatestAsync(Guid driverId, CancellationToken cancellationToken) { DriverLocationResponse? value = await store.GetLatestAsync(driverId, cancellationToken); return value is null ? Failure<DriverLocationResponse>(TrackingStatus.NotFound, "tracking.location_not_found") : new(TrackingStatus.Accepted, value); }
    public async Task<TrackingResult<DriverLocationHistoryResponse>> GetHistoryAsync(Guid driverId, DateTime fromUtc, DateTime toUtc, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc || fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromHours(settings.MaximumHistoryRangeHours) || page < 1 || pageSize is < 1 || pageSize > settings.MaximumHistoryPageSize) return Failure<DriverLocationHistoryResponse>(TrackingStatus.Rejected, "tracking.invalid_history_query");
        return new(TrackingStatus.Accepted, await store.GetHistoryAsync(driverId, fromUtc, toUtc, page, Math.Min(pageSize, settings.MaximumHistoryPageSize), cancellationToken));
    }
    private async Task<TrackingResult<LocationUpdateResponse>> Accept(DriverEligibilitySnapshot driver, LocationUpdateRequest request, LocationSource source, LocationBatchId? batchId, CancellationToken ct)
    {
        DateTime now = clock.UtcNow;
        try
        {
            LocationPosition position = LocationPosition.Create(request.Latitude, request.Longitude);
            DriverLocation location = DriverLocation.Create(DriverLocationId.New(), driver.DriverId, position, request.RecordedAtUtc, now, request.AccuracyMeters, request.SpeedMetersPerSecond, request.HeadingDegrees, request.AltitudeMeters, request.SequenceNumber, source, batchId);
            if (request.RecordedAtUtc > now.AddSeconds(settings.FutureToleranceSeconds) || request.RecordedAtUtc < now.AddHours(-settings.OfflineSyncWindowHours)) return Failure<LocationUpdateResponse>(TrackingStatus.Rejected, "tracking.timestamp_out_of_range");
            bool promote = request.AccuracyMeters <= settings.MaximumAccuracyMeters && (source == LocationSource.OfflineBatch || request.RecordedAtUtc >= now.AddSeconds(-settings.MaximumLiveStalenessSeconds));
            DriverLatestLocation? latest = await store.GetLatestEntityAsync(driver.DriverId, ct);
            if (latest is not null && request.RecordedAtUtc > latest.RecordedAtUtc && !MovementPlausibility.IsPlausible(latest.Position, position, request.RecordedAtUtc - latest.RecordedAtUtc, latest.AccuracyMeters, request.AccuracyMeters, settings.MaximumPlausibleSpeedMetersPerSecond)) promote = false;
            StoreLocationResult stored = await store.StoreAsync(location, promote, ct);
            string status = stored.Duplicate ? "duplicate" : stored.LatestUpdated ? "accepted" : "history-only";
            if (stored.LatestUpdated) await realtime.PublishAsync(driver.DriverId, new(position.Latitude, position.Longitude, request.RecordedAtUtc, request.AccuracyMeters, request.SpeedMetersPerSecond, request.HeadingDegrees), ct);
            return new(stored.Duplicate ? TrackingStatus.Duplicate : stored.LatestUpdated ? TrackingStatus.Accepted : TrackingStatus.HistoryOnly, new((stored.LocationId ?? location.Id).Value, request.SequenceNumber, status, stored.LatestUpdated, Interval(driver.AvailabilityStatus)));
        }
        catch (DomainException) { return Failure<LocationUpdateResponse>(TrackingStatus.Rejected, "tracking.invalid_location"); }
    }
    private async Task<DriverEligibilitySnapshot?> Resolve(TrackingActor actor, CancellationToken ct) { if (actor.UserId == Guid.Empty) return null; DriverEligibilitySnapshot? driver = await drivers.GetByUserAsync(actor.UserId, ct); return driver is { IsActive: true, IsApproved: true, HasActiveSuspension: false } ? driver : null; }
    private int Interval(short availability) => availability switch { 1 => settings.OfflineIntervalSeconds, 3 => settings.BusyIntervalSeconds, _ => settings.IdleIntervalSeconds };
    private static TrackingResult<T> Failure<T>(TrackingStatus status, string code) => new(status, default, code);
}
