using AlSsareea.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;

namespace AlSsareea.Modules.Customers.Infrastructure.Persistence;

internal static class CustomerConfigurationExtensions
{
    internal static PropertyBuilder<TId> CustomerId<TId>(this PropertyBuilder<TId> property, Func<TId, Guid> toGuid, Func<Guid, TId> fromGuid) where TId : struct =>
        property.HasConversion(value => toGuid(value), value => fromGuid(value)).HasColumnType("uuid").ValueGeneratedNever();
    internal static PropertyBuilder<DateTime> Utc(this PropertyBuilder<DateTime> property) => property.HasColumnType("timestamp with time zone");
    internal static PropertyBuilder<DateTime?> Utc(this PropertyBuilder<DateTime?> property) => property.HasColumnType("timestamp with time zone");
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers", CustomersPersistenceConstants.Schema, t =>
        {
            t.HasCheckConstraint("ck_customers_status", "status BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_customers_deleted_status", "(status = 4 AND deleted_at_utc IS NOT NULL) OR (status <> 4 AND deleted_at_utc IS NULL)");
            t.HasCheckConstraint("ck_customers_block_metadata", "(status = 3 AND block_reason IS NOT NULL AND blocked_at_utc IS NOT NULL AND blocked_by_user_id IS NOT NULL) OR status <> 3");
        });
        b.HasKey(x => x.Id).HasName("pk_customers");
        b.Property(x => x.Id).CustomerId(x => x.Value, x => new CustomerId(x));
        b.Property(x => x.UserId).HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.DateOfBirth).HasColumnType("date");
        b.Property(x => x.Status).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.BlockReason).HasMaxLength(1000);
        b.Property(x => x.BlockedAtUtc).Utc();
        b.Property(x => x.BlockedByUserId).HasColumnType("uuid");
        b.Property(x => x.CreatedAtUtc).Utc(); b.Property(x => x.UpdatedAtUtc).Utc(); b.Property(x => x.DeletedAtUtc).Utc();
        b.Property(x => x.CreatedByUserId).HasColumnType("uuid"); b.Property(x => x.UpdatedByUserId).HasColumnType("uuid"); b.Property(x => x.DeletedByUserId).HasColumnType("uuid");
        b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_customers_user_id");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_customers_status");
        b.HasIndex(x => new { x.CreatedAtUtc, x.Id }).HasDatabaseName("ix_customers_created_at_utc_id");
        b.HasIndex(x => new { x.UpdatedAtUtc, x.Id }).HasDatabaseName("ix_customers_updated_at_utc_id");
        b.HasQueryFilter(x => x.DeletedAtUtc == null);
        b.Ignore(x => x.DomainEvents);
        b.HasMany(x => x.Addresses).WithOne().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_addresses_customers_customer_id");
        b.HasMany(x => x.StatusHistory).WithOne().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_status_history_customers_customer_id");
        b.HasOne(x => x.Preferences).WithOne().HasForeignKey<CustomerPreference>(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_preferences_customers_customer_id");
    }
}

internal sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> b)
    {
        b.ToTable("customer_addresses", CustomersPersistenceConstants.Schema, t =>
        {
            t.HasCheckConstraint("ck_customer_addresses_type", "address_type BETWEEN 1 AND 3");
            t.HasCheckConstraint("ck_customer_addresses_location", "location IS NULL OR ST_SRID(location) = 4326");
        });
        b.HasKey(x => x.Id).HasName("pk_customer_addresses");
        b.Property(x => x.Id).CustomerId(x => x.Value, x => new CustomerAddressId(x));
        b.Property(x => x.CustomerId).CustomerId(x => x.Value, x => new CustomerId(x));
        b.Property(x => x.Label).HasMaxLength(100).IsRequired();
        b.Property(x => x.AddressType).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.City).HasMaxLength(150).IsRequired();
        b.Property(x => x.Area).HasMaxLength(150);
        b.Property(x => x.Street).HasMaxLength(200).IsRequired();
        b.Property(x => x.BuildingNumber).HasMaxLength(50);
        b.Property(x => x.Floor).HasMaxLength(30);
        b.Property(x => x.Apartment).HasMaxLength(30);
        b.Property(x => x.PostalCode).HasMaxLength(20);
        b.Property(x => x.PlaceId).HasMaxLength(300);
        var converter = new ValueConverter<GeoCoordinate?, Point?>(
            coordinate => ToPoint(coordinate),
            point => ToCoordinate(point));
        b.Property(x => x.Location).HasConversion(converter).HasColumnType("geometry (point, 4326)");
        b.Property(x => x.DeliveryInstructions).HasMaxLength(1000);
        b.Property(x => x.CreatedAtUtc).Utc(); b.Property(x => x.UpdatedAtUtc).Utc(); b.Property(x => x.DeletedAtUtc).Utc();
        b.Property(x => x.CreatedByUserId).HasColumnType("uuid"); b.Property(x => x.UpdatedByUserId).HasColumnType("uuid"); b.Property(x => x.DeletedByUserId).HasColumnType("uuid");
        b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => x.CustomerId).HasDatabaseName("ix_customer_addresses_customer_id");
        b.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc, x.Id }).HasDatabaseName("ix_customer_addresses_customer_id_created_at_utc_id");
        b.HasIndex(x => x.Location).HasMethod("gist").HasDatabaseName("ix_customer_addresses_location_gist");
        b.HasIndex(x => x.CustomerId).IsUnique().HasFilter("is_default = true AND deleted_at_utc IS NULL").HasDatabaseName("ux_customer_addresses_active_default");
        b.HasQueryFilter(x => x.DeletedAtUtc == null);
    }

    private static Point? ToPoint(GeoCoordinate? coordinate)
        => coordinate is null ? null : new Point(coordinate.Value.Longitude, coordinate.Value.Latitude) { SRID = 4326 };
    private static GeoCoordinate? ToCoordinate(Point? point)
        => point is null ? null : new GeoCoordinate(point.Y, point.X);
}

internal sealed class CustomerPreferenceConfiguration : IEntityTypeConfiguration<CustomerPreference>
{
    public void Configure(EntityTypeBuilder<CustomerPreference> b)
    {
        b.ToTable("customer_preferences", CustomersPersistenceConstants.Schema, t =>
        {
            t.HasCheckConstraint("ck_customer_preferences_language", "preferred_language IN ('ar', 'he', 'en')");
            t.HasCheckConstraint("ck_customer_preferences_currency", "preferred_currency ~ '^[A-Z]{3}$'");
        });
        b.HasKey(x => x.Id).HasName("pk_customer_preferences");
        b.Property(x => x.Id).CustomerId(x => x.Value, x => new CustomerPreferenceId(x));
        b.Property(x => x.CustomerId).CustomerId(x => x.Value, x => new CustomerId(x));
        b.Property(x => x.PreferredLanguage).HasMaxLength(2).IsRequired();
        b.Property(x => x.PreferredCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.CreatedAtUtc).Utc(); b.Property(x => x.UpdatedAtUtc).Utc();
        b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => x.CustomerId).IsUnique().HasDatabaseName("ux_customer_preferences_customer_id");
    }
}

internal sealed class CustomerStatusHistoryConfiguration : IEntityTypeConfiguration<CustomerStatusHistory>
{
    public void Configure(EntityTypeBuilder<CustomerStatusHistory> b)
    {
        b.ToTable("customer_status_history", CustomersPersistenceConstants.Schema, t =>
        {
            t.HasCheckConstraint("ck_customer_status_history_previous_status", "previous_status BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_customer_status_history_new_status", "new_status BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_customer_status_history_reason", "new_status = 1 OR reason IS NOT NULL");
        });
        b.HasKey(x => x.Id).HasName("pk_customer_status_history");
        b.Property(x => x.Id).CustomerId(x => x.Value, x => new CustomerStatusHistoryId(x));
        b.Property(x => x.CustomerId).CustomerId(x => x.Value, x => new CustomerId(x));
        b.Property(x => x.PreviousStatus).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.NewStatus).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ChangedAtUtc).Utc();
        b.Property(x => x.ChangedByUserId).HasColumnType("uuid");
        b.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
        b.HasIndex(x => new { x.CustomerId, x.ChangedAtUtc, x.Id }).IsDescending(false, true, false).HasDatabaseName("ix_customer_status_history_customer_id_changed_at_utc_id");
    }
}
