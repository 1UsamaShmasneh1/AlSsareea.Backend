using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Tracking.Domain;

public readonly record struct DriverLocationId
{
    public DriverLocationId(Guid value) { if (value == Guid.Empty) throw new DomainException("Location identifier is required."); Value = value; }
    public Guid Value { get; }
    public static DriverLocationId New() => new(Guid.NewGuid());
}

public readonly record struct LocationBatchId
{
    public LocationBatchId(Guid value) { if (value == Guid.Empty) throw new DomainException("Batch identifier is required."); Value = value; }
    public Guid Value { get; }
}

public readonly record struct LocationPosition
{
    private LocationPosition(double latitude, double longitude) { Latitude = latitude; Longitude = longitude; }
    public double Latitude { get; }
    public double Longitude { get; }
    public static LocationPosition Create(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90) throw new DomainException("Latitude must be finite and between -90 and 90.");
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180) throw new DomainException("Longitude must be finite and between -180 and 180.");
        return new(latitude, longitude);
    }
}

public enum LocationSource : short { Live = 1, OfflineBatch = 2 }

public sealed class DriverLocation : Entity<DriverLocationId>
{
    private DriverLocation() : base(default) { }
    private DriverLocation(DriverLocationId id, Guid driverId, LocationPosition position, DateTime recordedAtUtc, DateTime receivedAtUtc, double accuracyMeters, double? speedMetersPerSecond, double? headingDegrees, double? altitudeMeters, long sequenceNumber, LocationSource source, LocationBatchId? batchId)
        : base(id)
    {
        DriverId = driverId; Position = position; RecordedAtUtc = recordedAtUtc; ReceivedAtUtc = receivedAtUtc; AccuracyMeters = accuracyMeters; SpeedMetersPerSecond = speedMetersPerSecond; HeadingDegrees = headingDegrees; AltitudeMeters = altitudeMeters; SequenceNumber = sequenceNumber; Source = source; BatchId = batchId; CreatedAtUtc = receivedAtUtc;
    }
    public Guid DriverId { get; private set; }
    public LocationPosition Position { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public double AccuracyMeters { get; private set; }
    public double? SpeedMetersPerSecond { get; private set; }
    public double? HeadingDegrees { get; private set; }
    public double? AltitudeMeters { get; private set; }
    public long SequenceNumber { get; private set; }
    public LocationSource Source { get; private set; }
    public LocationBatchId? BatchId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static DriverLocation Create(DriverLocationId id, Guid driverId, LocationPosition position, DateTime recordedAtUtc, DateTime receivedAtUtc, double accuracyMeters, double? speedMetersPerSecond, double? headingDegrees, double? altitudeMeters, long sequenceNumber, LocationSource source, LocationBatchId? batchId = null)
    {
        if (driverId == Guid.Empty) throw new DomainException("Driver identifier is required.");
        RequireUtc(recordedAtUtc, "Recorded timestamp"); RequireUtc(receivedAtUtc, "Received timestamp");
        if (!double.IsFinite(accuracyMeters) || accuracyMeters <= 0) throw new DomainException("Accuracy must be positive and finite.");
        if (speedMetersPerSecond is { } speed && (!double.IsFinite(speed) || speed < 0)) throw new DomainException("Speed cannot be negative or non-finite.");
        if (headingDegrees is { } heading && (!double.IsFinite(heading) || heading is < 0 or >= 360)) throw new DomainException("Heading must be between 0 inclusive and 360 exclusive.");
        if (altitudeMeters is { } altitude && !double.IsFinite(altitude)) throw new DomainException("Altitude must be finite.");
        if (sequenceNumber < 0) throw new DomainException("Sequence number cannot be negative.");
        return new(id, driverId, position, recordedAtUtc, receivedAtUtc, accuracyMeters, speedMetersPerSecond, headingDegrees, altitudeMeters, sequenceNumber, source, batchId);
    }
    private static void RequireUtc(DateTime value, string name) { if (value.Kind != DateTimeKind.Utc) throw new DomainException($"{name} must be UTC."); }
}

public sealed class DriverLatestLocation
{
    private DriverLatestLocation() { }
    private DriverLatestLocation(DriverLocation location, DateTime updatedAtUtc) { Apply(location, updatedAtUtc); ConcurrencyStamp = Guid.NewGuid(); }
    public Guid DriverId { get; private set; }
    public DriverLocationId LocationId { get; private set; }
    public LocationPosition Position { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public double AccuracyMeters { get; private set; }
    public double? SpeedMetersPerSecond { get; private set; }
    public double? HeadingDegrees { get; private set; }
    public long LastSequenceNumber { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public static DriverLatestLocation Create(DriverLocation location, DateTime updatedAtUtc) => new(location, updatedAtUtc);
    public bool IsNewer(DriverLocation incoming) => incoming.SequenceNumber > LastSequenceNumber || (incoming.SequenceNumber == LastSequenceNumber && incoming.RecordedAtUtc > RecordedAtUtc);
    public bool TryPromote(DriverLocation incoming, DateTime updatedAtUtc) { if (!IsNewer(incoming)) return false; Apply(incoming, updatedAtUtc); ConcurrencyStamp = Guid.NewGuid(); return true; }
    private void Apply(DriverLocation location, DateTime updatedAtUtc) { DriverId = location.DriverId; LocationId = location.Id; Position = location.Position; RecordedAtUtc = location.RecordedAtUtc; ReceivedAtUtc = location.ReceivedAtUtc; AccuracyMeters = location.AccuracyMeters; SpeedMetersPerSecond = location.SpeedMetersPerSecond; HeadingDegrees = location.HeadingDegrees; LastSequenceNumber = location.SequenceNumber; UpdatedAtUtc = updatedAtUtc; }
}

public static class MovementPlausibility
{
    private const double EarthRadiusMeters = 6_371_000;
    public static double DistanceMeters(LocationPosition from, LocationPosition to)
    {
        double lat1 = Degrees(from.Latitude), lat2 = Degrees(to.Latitude), dLat = lat2 - lat1, dLon = Degrees(to.Longitude - from.Longitude);
        double a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
    public static bool IsPlausible(LocationPosition from, LocationPosition to, TimeSpan elapsed, double previousAccuracy, double incomingAccuracy, double maximumSpeedMetersPerSecond)
    {
        if (elapsed <= TimeSpan.Zero) return true;
        double adjustedDistance = Math.Max(0, DistanceMeters(from, to) - previousAccuracy - incomingAccuracy);
        return adjustedDistance / elapsed.TotalSeconds <= maximumSpeedMetersPerSecond;
    }
    private static double Degrees(double value) => value * Math.PI / 180;
}
