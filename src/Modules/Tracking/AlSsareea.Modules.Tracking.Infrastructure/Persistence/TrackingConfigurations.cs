using AlSsareea.Modules.Tracking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;

namespace AlSsareea.Modules.Tracking.Infrastructure.Persistence;

internal static class TrackingConversions
{
    public static readonly ValueConverter<DriverLocationId, Guid> LocationId = new(x => x.Value, x => new(x));
    public static readonly ValueConverter<LocationBatchId, Guid> BatchId = new(x => x.Value, x => new(x));
    public static readonly ValueConverter<LocationPosition, Point> Position = new(x => new Point(x.Longitude, x.Latitude) { SRID = 4326 }, x => LocationPosition.Create(x.Y, x.X));
    public static readonly ValueComparer<LocationPosition> PositionComparer = new((a, b) => a.Latitude == b.Latitude && a.Longitude == b.Longitude, x => HashCode.Combine(x.Latitude, x.Longitude), x => x);
}

internal sealed class DriverLocationConfiguration : IEntityTypeConfiguration<DriverLocation>
{
    public void Configure(EntityTypeBuilder<DriverLocation> builder)
    {
        builder.ToTable("driver_locations", table =>
        {
            table.HasCheckConstraint("ck_driver_locations_accuracy", "accuracy_meters > 0");
            table.HasCheckConstraint("ck_driver_locations_speed", "speed_meters_per_second IS NULL OR speed_meters_per_second >= 0");
            table.HasCheckConstraint("ck_driver_locations_heading", "heading_degrees IS NULL OR (heading_degrees >= 0 AND heading_degrees < 360)");
            table.HasCheckConstraint("ck_driver_locations_sequence", "sequence_number >= 0");
            table.HasCheckConstraint("ck_driver_locations_position_srid", "ST_SRID(position) = 4326");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasConversion(TrackingConversions.LocationId).ValueGeneratedNever();
        builder.Property(x => x.DriverId).IsRequired();
        builder.Property(x => x.Position).HasConversion(TrackingConversions.Position, TrackingConversions.PositionComparer).HasColumnType("geometry(Point,4326)").IsRequired();
        builder.Property(x => x.RecordedAtUtc).HasColumnType("timestamp with time zone"); builder.Property(x => x.ReceivedAtUtc).HasColumnType("timestamp with time zone"); builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.Source).HasConversion<short>();
        builder.Property(x => x.BatchId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new LocationBatchId(x.Value) : null);
        builder.HasIndex(x => new { x.DriverId, x.SequenceNumber }).IsUnique();
        builder.HasIndex(x => new { x.DriverId, x.RecordedAtUtc }).IsDescending(false, true);
        builder.HasIndex(x => x.RecordedAtUtc); builder.HasIndex(x => x.ReceivedAtUtc);
        builder.HasIndex(x => x.Position).HasMethod("gist");
    }
}

internal sealed class DriverLatestLocationConfiguration : IEntityTypeConfiguration<DriverLatestLocation>
{
    public void Configure(EntityTypeBuilder<DriverLatestLocation> builder)
    {
        builder.ToTable("driver_latest_locations", table =>
        {
            table.HasCheckConstraint("ck_driver_latest_locations_accuracy", "accuracy_meters > 0");
            table.HasCheckConstraint("ck_driver_latest_locations_speed", "speed_meters_per_second IS NULL OR speed_meters_per_second >= 0");
            table.HasCheckConstraint("ck_driver_latest_locations_heading", "heading_degrees IS NULL OR (heading_degrees >= 0 AND heading_degrees < 360)");
            table.HasCheckConstraint("ck_driver_latest_locations_sequence", "last_sequence_number >= 0");
            table.HasCheckConstraint("ck_driver_latest_locations_position_srid", "ST_SRID(position) = 4326");
        });
        builder.HasKey(x => x.DriverId); builder.Property(x => x.DriverId).ValueGeneratedNever();
        builder.Property(x => x.LocationId).HasConversion(TrackingConversions.LocationId);
        builder.Property(x => x.Position).HasConversion(TrackingConversions.Position, TrackingConversions.PositionComparer).HasColumnType("geometry(Point,4326)").IsRequired();
        builder.Property(x => x.RecordedAtUtc).HasColumnType("timestamp with time zone"); builder.Property(x => x.ReceivedAtUtc).HasColumnType("timestamp with time zone"); builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        builder.HasIndex(x => x.RecordedAtUtc); builder.HasIndex(x => x.Position).HasMethod("gist");
    }
}
