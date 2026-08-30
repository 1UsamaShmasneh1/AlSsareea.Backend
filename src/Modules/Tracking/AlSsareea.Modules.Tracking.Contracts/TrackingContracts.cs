namespace AlSsareea.Modules.Tracking.Contracts;

public static class TrackingPermissions
{
    public const string UpdateSelf = "tracking.locations.update.self";
    public const string Read = "tracking.locations.read";
    public const string ReadHistory = "tracking.locations.read.history";
    public const string RealtimeOperations = "tracking.realtime.operations";
}

public sealed record LocationUpdateRequest(DateTime RecordedAtUtc, double Latitude, double Longitude, double AccuracyMeters, double? SpeedMetersPerSecond, double? HeadingDegrees, double? AltitudeMeters, long SequenceNumber);
public sealed record LocationBatchRequest(Guid BatchId, IReadOnlyList<LocationUpdateRequest> Locations);
public sealed record LocationUpdateResponse(Guid LocationId, long SequenceNumber, string Status, bool LatestUpdated, int RecommendedUpdateIntervalSeconds);
public sealed record LocationBatchResponse(Guid BatchId, int Accepted, int HistoryOnly, int Duplicates, int Rejected, IReadOnlyList<LocationUpdateResponse> Results);
public sealed record DriverLocationResponse(Guid LocationId, Guid DriverId, double Latitude, double Longitude, DateTime RecordedAtUtc, DateTime ReceivedAtUtc, double AccuracyMeters, double? SpeedMetersPerSecond, double? HeadingDegrees, long SequenceNumber);
public sealed record DriverLocationHistoryResponse(IReadOnlyList<DriverLocationResponse> Items, int Page, int PageSize, int TotalCount);
public sealed record TrackingRealtimePayload(double Latitude, double Longitude, DateTime RecordedAtUtc, double AccuracyMeters, double? SpeedMetersPerSecond, double? HeadingDegrees);
public sealed record TrackingVisibility(Guid DriverId, string AudienceKey);
public interface ITrackingVisibilityProvider { Task<TrackingVisibility?> ResolveOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default); }
public interface ITrackingOrderAudienceProvider
{
    Task<IReadOnlyList<Guid>> GetVisibleOrderIdsForDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
}
public sealed record DispatchDriverLocation(Guid DriverId, double Latitude, double Longitude, DateTime RecordedAtUtc, double AccuracyMeters);
public interface IDispatchLocationProvider { Task<DispatchDriverLocation?> GetLatestAsync(Guid driverId, CancellationToken cancellationToken = default); }
