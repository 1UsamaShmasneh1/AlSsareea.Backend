using AlSsareea.Modules.Merchants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace AlSsareea.Modules.Merchants.Infrastructure.Persistence;

internal static class MerchantConfigurationExtensions
{
    internal static PropertyBuilder<TId> Id<TId>(this PropertyBuilder<TId> property, Func<TId, Guid> toGuid, Func<Guid, TId> fromGuid) where TId : struct =>
        property.HasConversion(value => toGuid(value), value => fromGuid(value)).HasColumnType("uuid").ValueGeneratedNever();
    internal static PropertyBuilder<DateTime> Utc(this PropertyBuilder<DateTime> property) => property.HasColumnType("timestamp with time zone");
    internal static PropertyBuilder<DateTime?> Utc(this PropertyBuilder<DateTime?> property) => property.HasColumnType("timestamp with time zone");
}

internal sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> b)
    {
        b.ToTable("merchants", MerchantsPersistence.Schema, t => t.HasCheckConstraint("ck_merchants_status", "status BETWEEN 1 AND 5"));
        b.HasKey(x => x.Id).HasName("pk_merchants");
        b.Property(x => x.Id).Id(x => x.Value, x => new MerchantId(x));
        b.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.RegistrationNumber).HasMaxLength(100);
        b.Property(x => x.TaxNumber).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
        b.Property(x => x.OwnerUserId).HasColumnType("uuid");
        b.Property(x => x.Status).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.CreatedAtUtc).Utc(); b.Property(x => x.UpdatedAtUtc).Utc(); b.Property(x => x.ActivatedAtUtc).Utc();
        b.Property(x => x.SuspendedAtUtc).Utc(); b.Property(x => x.RejectedAtUtc).Utc(); b.Property(x => x.ClosedAtUtc).Utc();
        b.Property(x => x.SuspensionReason).HasMaxLength(1000); b.Property(x => x.RejectionReason).HasMaxLength(1000); b.Property(x => x.ClosingReason).HasMaxLength(1000);
        b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => x.Status).HasDatabaseName("ix_merchants_status");
        b.HasIndex(x => x.OwnerUserId).HasDatabaseName("ix_merchants_owner_user_id");
        b.HasIndex(x => x.DisplayName).HasDatabaseName("ix_merchants_display_name");
        b.Ignore(x => x.DomainEvents);
    }
}

internal sealed class MerchantBranchConfiguration : IEntityTypeConfiguration<MerchantBranch>
{
    public void Configure(EntityTypeBuilder<MerchantBranch> b)
    {
        b.ToTable("merchant_branches", MerchantsPersistence.Schema, t => t.HasCheckConstraint("ck_merchant_branches_status", "status BETWEEN 1 AND 5"));
        b.HasKey(x => x.Id).HasName("pk_merchant_branches");
        b.Property(x => x.Id).Id(x => x.Value, x => new MerchantBranchId(x));
        b.Property(x => x.MerchantId).Id(x => x.Value, x => new MerchantId(x));
        b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired(); b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Status).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        b.Property(x => x.Location).HasConversion(
            value => new Point(value.Longitude, value.Latitude) { SRID = 4326 },
            value => new GeoCoordinate(value.Y, value.X)).HasColumnType("geometry(Point,4326)").IsRequired();
        b.OwnsOne(x => x.Address, address =>
        {
            address.Property(x => x.City).HasColumnName("address_city").HasMaxLength(150).IsRequired();
            address.Property(x => x.Area).HasColumnName("address_area").HasMaxLength(150);
            address.Property(x => x.Street).HasColumnName("address_street").HasMaxLength(200).IsRequired();
            address.Property(x => x.BuildingNumber).HasColumnName("address_building_number").HasMaxLength(50);
            address.Property(x => x.PostalCode).HasColumnName("address_postal_code").HasMaxLength(20);
        });
        b.Property(x => x.CreatedAtUtc).Utc(); b.Property(x => x.UpdatedAtUtc).Utc(); b.Property(x => x.ActivatedAtUtc).Utc();
        b.Property(x => x.TemporarilyClosedAtUtc).Utc(); b.Property(x => x.ReopenedAtUtc).Utc(); b.Property(x => x.SuspendedAtUtc).Utc(); b.Property(x => x.ClosedAtUtc).Utc();
        b.Property(x => x.StatusChangeReason).HasMaxLength(1000);
        b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => x.MerchantId).HasDatabaseName("ix_merchant_branches_merchant_id");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_merchant_branches_status");
        b.HasIndex(x => new { x.MerchantId, x.Code }).IsUnique().HasFilter("code IS NOT NULL").HasDatabaseName("ux_merchant_branches_merchant_id_code");
        b.HasIndex(x => x.MerchantId).IsUnique().HasFilter("is_primary = true").HasDatabaseName("ux_merchant_branches_primary_per_merchant");
        b.HasIndex(x => x.Location).HasMethod("gist").HasDatabaseName("ix_merchant_branches_location_gist");
        b.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_merchant_branches_merchants_merchant_id");
        b.HasMany(x => x.BusinessHours).WithOne().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.ScheduleOverrides).WithOne().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.ServiceAreas).WithOne().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.DomainEvents);
    }
}

internal sealed class MerchantEmployeeConfiguration : IEntityTypeConfiguration<MerchantEmployee>
{
    public void Configure(EntityTypeBuilder<MerchantEmployee> b)
    {
        b.ToTable("merchant_employees", MerchantsPersistence.Schema, t =>
        {
            t.HasCheckConstraint("ck_merchant_employees_role", "role BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_merchant_employees_status", "status BETWEEN 1 AND 4");
        });
        b.HasKey(x => x.Id).HasName("pk_merchant_employees");
        b.Property(x => x.Id).Id(x => x.Value, x => new MerchantEmployeeId(x));
        b.Property(x => x.MerchantId).Id(x => x.Value, x => new MerchantId(x));
        b.Property(x => x.BranchId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new MerchantBranchId(x.Value) : null).HasColumnType("uuid");
        b.Property(x => x.UserId).HasColumnType("uuid"); b.Property(x => x.Role).HasConversion<short>().HasColumnType("smallint"); b.Property(x => x.Status).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.JoinedAtUtc).Utc(); b.Property(x => x.SuspendedAtUtc).Utc(); b.Property(x => x.RemovedAtUtc).Utc(); b.Property(x => x.CreatedAtUtc).Utc(); b.Property(x => x.UpdatedAtUtc).Utc();
        b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_merchant_employees_user_id");
        b.HasIndex(x => new { x.MerchantId, x.UserId }).IsUnique().HasFilter("status <> 4").HasDatabaseName("ux_merchant_employees_active_user");
        b.HasIndex(x => x.MerchantId).IsUnique().HasFilter("role = 1 AND status = 2").HasDatabaseName("ux_merchant_employees_active_owner");
        b.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MerchantBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.DomainEvents);
    }
}

internal sealed class BusinessHourConfiguration : IEntityTypeConfiguration<BusinessHour>
{
    public void Configure(EntityTypeBuilder<BusinessHour> b)
    {
        b.ToTable("merchant_business_hours", MerchantsPersistence.Schema, t => t.HasCheckConstraint("ck_business_hours_day", "day_of_week BETWEEN 0 AND 6"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).Id(x => x.Value, x => new BusinessHourId(x)); b.Property(x => x.BranchId).Id(x => x.Value, x => new MerchantBranchId(x));
        b.Property(x => x.DayOfWeek).HasConversion<short>().HasColumnType("smallint");
        b.HasIndex(x => new { x.BranchId, x.DayOfWeek }).IsUnique();
        b.HasMany(x => x.Periods).WithOne().HasForeignKey(x => x.BusinessHourId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BusinessHourPeriodConfiguration : IEntityTypeConfiguration<BusinessHourPeriod>
{
    public void Configure(EntityTypeBuilder<BusinessHourPeriod> b)
    {
        b.ToTable("merchant_business_hour_periods", MerchantsPersistence.Schema, t => t.HasCheckConstraint("ck_business_hour_period_order", "opens_at < closes_at"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).Id(x => x.Value, x => new BusinessHourPeriodId(x)); b.Property(x => x.BusinessHourId).Id(x => x.Value, x => new BusinessHourId(x));
        b.Property(x => x.OpensAt).HasColumnType("time without time zone"); b.Property(x => x.ClosesAt).HasColumnType("time without time zone");
        b.HasIndex(x => new { x.BusinessHourId, x.OpensAt });
    }
}

internal sealed class ScheduleOverrideConfiguration : IEntityTypeConfiguration<BranchScheduleOverride>
{
    public void Configure(EntityTypeBuilder<BranchScheduleOverride> b)
    {
        b.ToTable("merchant_branch_schedule_overrides", MerchantsPersistence.Schema, t => t.HasCheckConstraint("ck_schedule_override_dates", "end_date >= start_date"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).Id(x => x.Value, x => new ScheduleOverrideId(x)); b.Property(x => x.BranchId).Id(x => x.Value, x => new MerchantBranchId(x));
        b.Property(x => x.StartDate).HasColumnType("date"); b.Property(x => x.EndDate).HasColumnType("date"); b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).Utc(); b.Property(x => x.CancelledAtUtc).Utc();
        b.HasIndex(x => new { x.BranchId, x.StartDate, x.EndDate });
        b.HasMany(x => x.Periods).WithOne().HasForeignKey(x => x.ScheduleOverrideId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SpecialHourPeriodConfiguration : IEntityTypeConfiguration<SpecialHourPeriod>
{
    public void Configure(EntityTypeBuilder<SpecialHourPeriod> b)
    {
        b.ToTable("merchant_branch_special_hour_periods", MerchantsPersistence.Schema, t => t.HasCheckConstraint("ck_special_hour_period_order", "opens_at < closes_at"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).Id(x => x.Value, x => new SpecialHourPeriodId(x)); b.Property(x => x.ScheduleOverrideId).Id(x => x.Value, x => new ScheduleOverrideId(x));
        b.Property(x => x.OpensAt).HasColumnType("time without time zone"); b.Property(x => x.ClosesAt).HasColumnType("time without time zone");
    }
}

internal sealed class BranchServiceAreaConfiguration : IEntityTypeConfiguration<BranchServiceArea>
{
    public void Configure(EntityTypeBuilder<BranchServiceArea> b)
    {
        b.ToTable("merchant_branch_service_areas", MerchantsPersistence.Schema);
        b.HasKey(x => new { x.BranchId, x.ServiceAreaId }); b.Property(x => x.BranchId).Id(x => x.Value, x => new MerchantBranchId(x)); b.Property(x => x.ServiceAreaId).HasColumnType("uuid");
        b.Property(x => x.AssignedAtUtc).Utc(); b.HasIndex(x => x.ServiceAreaId);
    }
}
