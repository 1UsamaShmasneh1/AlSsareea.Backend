using AlSsareea.Modules.Drivers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Drivers.Infrastructure.Persistence;

internal static class DriverConfigurationExtensions
{
    internal static PropertyBuilder<T> StrongId<T>(this PropertyBuilder<T> property, Func<T, Guid> toGuid, Func<Guid, T> fromGuid) => property.HasConversion(value => toGuid(value), value => fromGuid(value)).HasColumnType("uuid").ValueGeneratedNever();
}

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> b)
    {
        b.ToTable("drivers", DriversPersistence.Schema, t => { t.HasCheckConstraint("ck_drivers_capacity", "maximum_concurrent_deliveries > 0 AND current_load >= 0 AND current_load <= maximum_concurrent_deliveries"); t.HasCheckConstraint("ck_drivers_status", "status BETWEEN 1 AND 6"); t.HasCheckConstraint("ck_drivers_activation_status", "activation_status BETWEEN 1 AND 5"); t.HasCheckConstraint("ck_drivers_availability_status", "availability_status BETWEEN 1 AND 5"); });
        b.HasKey(x => x.Id).HasName("pk_drivers"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DriverId(x)); b.Property(x => x.DisplayName).HasMaxLength(DriverRules.DisplayNameMaximumLength); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.ActivationStatus).HasConversion<short>(); b.Property(x => x.EmploymentType).HasConversion<short>(); b.Property(x => x.AvailabilityStatus).HasConversion<short>(); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.Ignore(x => x.DomainEvents);
        b.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_drivers_user_id"); b.HasIndex(x => x.Status).HasDatabaseName("ix_drivers_status"); b.HasIndex(x => x.ActivationStatus).HasDatabaseName("ix_drivers_activation_status"); b.HasIndex(x => x.AvailabilityStatus).HasDatabaseName("ix_drivers_availability_status"); b.HasIndex(x => x.EmploymentType).HasDatabaseName("ix_drivers_employment_type"); b.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("ix_drivers_created_at_utc"); b.HasIndex(x => x.UpdatedAtUtc).HasDatabaseName("ix_drivers_updated_at_utc");
        b.HasMany(x => x.Vehicles).WithOne().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_vehicles_drivers_driver_id"); b.Navigation(x => x.Vehicles).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Documents).WithOne().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_driver_documents_drivers_driver_id"); b.Navigation(x => x.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.ZoneAssignments).WithOne().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_driver_zones_drivers_driver_id"); b.Navigation(x => x.ZoneAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Shifts).WithOne().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_driver_shifts_drivers_driver_id"); b.Navigation(x => x.Shifts).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Violations).WithOne().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_driver_violations_drivers_driver_id"); b.Navigation(x => x.Violations).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Suspensions).WithOne().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_driver_suspensions_drivers_driver_id"); b.Navigation(x => x.Suspensions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b) { b.ToTable("vehicles", DriversPersistence.Schema, t => { t.HasCheckConstraint("ck_vehicles_type", "type BETWEEN 1 AND 5"); t.HasCheckConstraint("ck_vehicles_status", "status BETWEEN 1 AND 6"); t.HasCheckConstraint("ck_vehicles_year", "year IS NULL OR year BETWEEN 1980 AND 2100"); }); b.HasKey(x => x.Id).HasName("pk_vehicles"); b.Property(x => x.Id).StrongId(x => x.Value, x => new VehicleId(x)); b.Property(x => x.DriverId).StrongId(x => x.Value, x => new DriverId(x)); b.Property(x => x.Type).HasConversion<short>(); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.Make).HasMaxLength(100); b.Property(x => x.Model).HasMaxLength(100); b.Property(x => x.Color).HasMaxLength(50); b.Property(x => x.PlateNumber).HasMaxLength(DriverRules.PlateMaximumLength); b.Property(x => x.NormalizedPlateNumber).HasMaxLength(DriverRules.PlateMaximumLength); b.Property(x => x.RegistrationCountry).HasMaxLength(2).IsFixedLength(); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.HasIndex(x => x.DriverId).HasDatabaseName("ix_vehicles_driver_id"); b.HasIndex(x => x.Status).HasDatabaseName("ix_vehicles_status"); b.HasIndex(x => x.NormalizedPlateNumber).IsUnique().HasFilter("normalized_plate_number IS NOT NULL AND status <> 6").HasDatabaseName("ux_vehicles_active_plate"); b.HasIndex(x => x.DriverId).IsUnique().HasFilter("is_primary AND status = 2").HasDatabaseName("ux_vehicles_driver_primary_active"); }
}

internal sealed class DriverDocumentConfiguration : IEntityTypeConfiguration<DriverDocument>
{
    public void Configure(EntityTypeBuilder<DriverDocument> b) { b.ToTable("driver_documents", DriversPersistence.Schema, t => { t.HasCheckConstraint("ck_driver_documents_dates", "expires_at_utc IS NULL OR issued_at_utc IS NULL OR expires_at_utc > issued_at_utc"); t.HasCheckConstraint("ck_driver_documents_review", "status NOT IN (2, 3) OR reviewed_at_utc IS NOT NULL"); t.HasCheckConstraint("ck_driver_documents_rejection", "status <> 3 OR rejection_reason IS NOT NULL"); }); b.HasKey(x => x.Id).HasName("pk_driver_documents"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DriverDocumentId(x)); b.Property(x => x.DriverId).StrongId(x => x.Value, x => new DriverId(x)); b.Property(x => x.Type).HasConversion<short>(); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.RejectionReason).HasMaxLength(DriverRules.TextMaximumLength); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.HasIndex(x => x.DriverId).HasDatabaseName("ix_driver_documents_driver_id"); b.HasIndex(x => x.Type).HasDatabaseName("ix_driver_documents_type"); b.HasIndex(x => x.Status).HasDatabaseName("ix_driver_documents_status"); b.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("ix_driver_documents_expires_at_utc"); b.HasIndex(x => new { x.DriverId, x.Type }).IsUnique().HasFilter("status IN (1, 2)").HasDatabaseName("ux_driver_documents_current_type"); }
}

internal sealed class DriverZoneAssignmentConfiguration : IEntityTypeConfiguration<DriverZoneAssignment>
{
    public void Configure(EntityTypeBuilder<DriverZoneAssignment> b) { b.ToTable("driver_zone_assignments", DriversPersistence.Schema); b.HasKey(x => x.Id).HasName("pk_driver_zone_assignments"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DriverZoneAssignmentId(x)); b.Property(x => x.DriverId).StrongId(x => x.Value, x => new DriverId(x)); b.HasIndex(x => x.DriverId).HasDatabaseName("ix_driver_zones_driver_id"); b.HasIndex(x => x.ZoneId).HasDatabaseName("ix_driver_zones_zone_id"); b.HasIndex(x => new { x.DriverId, x.ZoneId }).IsUnique().HasFilter("is_active").HasDatabaseName("ux_driver_zones_active"); b.HasIndex(x => x.DriverId).IsUnique().HasFilter("is_active AND is_primary").HasDatabaseName("ux_driver_zones_primary"); }
}

internal sealed class DriverShiftConfiguration : IEntityTypeConfiguration<DriverShift>
{
    public void Configure(EntityTypeBuilder<DriverShift> b) { b.ToTable("driver_shifts", DriversPersistence.Schema, t => { t.HasCheckConstraint("ck_driver_shifts_scheduled", "scheduled_end_utc > scheduled_start_utc"); t.HasCheckConstraint("ck_driver_shifts_actual", "actual_end_utc IS NULL OR actual_start_utc IS NOT NULL AND actual_end_utc >= actual_start_utc"); }); b.HasKey(x => x.Id).HasName("pk_driver_shifts"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DriverShiftId(x)); b.Property(x => x.DriverId).StrongId(x => x.Value, x => new DriverId(x)); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.HasIndex(x => x.DriverId).HasDatabaseName("ix_driver_shifts_driver_id"); b.HasIndex(x => x.ScheduledStartUtc).HasDatabaseName("ix_driver_shifts_scheduled_start_utc"); b.HasIndex(x => x.Status).HasDatabaseName("ix_driver_shifts_status"); }
}

internal sealed class DriverViolationConfiguration : IEntityTypeConfiguration<DriverViolation>
{
    public void Configure(EntityTypeBuilder<DriverViolation> b) { b.ToTable("driver_violations", DriversPersistence.Schema); b.HasKey(x => x.Id).HasName("pk_driver_violations"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DriverViolationId(x)); b.Property(x => x.DriverId).StrongId(x => x.Value, x => new DriverId(x)); b.Property(x => x.ViolationType).HasMaxLength(DriverRules.CodeMaximumLength); b.Property(x => x.Description).HasMaxLength(DriverRules.TextMaximumLength); b.Property(x => x.ResolutionNotes).HasMaxLength(DriverRules.TextMaximumLength); b.Property(x => x.Severity).HasConversion<short>(); b.Property(x => x.Status).HasConversion<short>(); b.HasIndex(x => x.DriverId).HasDatabaseName("ix_driver_violations_driver_id"); b.HasIndex(x => x.OccurredAtUtc).HasDatabaseName("ix_driver_violations_occurred_at_utc"); b.HasIndex(x => x.Severity).HasDatabaseName("ix_driver_violations_severity"); b.HasIndex(x => x.Status).HasDatabaseName("ix_driver_violations_status"); }
}

internal sealed class DriverSuspensionConfiguration : IEntityTypeConfiguration<DriverSuspension>
{
    public void Configure(EntityTypeBuilder<DriverSuspension> b) { b.ToTable("driver_suspensions", DriversPersistence.Schema, t => t.HasCheckConstraint("ck_driver_suspensions_dates", "ends_at_utc IS NULL OR ends_at_utc > starts_at_utc")); b.HasKey(x => x.Id).HasName("pk_driver_suspensions"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DriverSuspensionId(x)); b.Property(x => x.DriverId).StrongId(x => x.Value, x => new DriverId(x)); b.Property(x => x.ReasonCode).HasMaxLength(DriverRules.CodeMaximumLength); b.Property(x => x.Reason).HasMaxLength(DriverRules.TextMaximumLength); b.Property(x => x.LiftReason).HasMaxLength(DriverRules.TextMaximumLength); b.Property(x => x.Status).HasConversion<short>(); b.HasIndex(x => x.DriverId).HasDatabaseName("ix_driver_suspensions_driver_id"); b.HasIndex(x => x.StartsAtUtc).HasDatabaseName("ix_driver_suspensions_starts_at_utc"); b.HasIndex(x => x.EndsAtUtc).HasDatabaseName("ix_driver_suspensions_ends_at_utc"); b.HasIndex(x => x.Status).HasDatabaseName("ix_driver_suspensions_status"); }
}

internal sealed class DriverIdempotencyConfiguration : IEntityTypeConfiguration<DriverIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<DriverIdempotencyRecord> b) { b.ToTable("idempotency_records", DriversPersistence.Schema, t => { t.HasCheckConstraint("ck_driver_idempotency_hashes", "char_length(key_hash) = 64 AND char_length(request_hash) = 64"); t.HasCheckConstraint("ck_driver_idempotency_response", "response_json IS NULL OR jsonb_typeof(response_json) = 'object'"); }); b.HasKey(x => x.Id).HasName("pk_driver_idempotency"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DriverIdempotencyId(x)); b.Property(x => x.DriverId).StrongId(x => x.Value, x => new DriverId(x)); b.Property(x => x.Operation).HasMaxLength(100); b.Property(x => x.KeyHash).HasMaxLength(64).IsFixedLength(); b.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength(); b.Property(x => x.ResponseStatus).HasConversion<short?>(); b.Property(x => x.ResponseJson).HasColumnType("jsonb"); b.HasIndex(x => new { x.ActorUserId, x.Operation, x.KeyHash }).IsUnique().HasDatabaseName("ux_driver_idempotency_scope"); }
}

internal sealed class DriverAuditConfiguration : IEntityTypeConfiguration<DriverAuditRecord>
{
    public void Configure(EntityTypeBuilder<DriverAuditRecord> b) { b.ToTable("audit_records", DriversPersistence.Schema); b.HasKey(x => x.Id).HasName("pk_driver_audit"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DriverAuditId(x)); b.Property(x => x.DriverId).StrongId(x => x.Value, x => new DriverId(x)); b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.CorrelationId).HasMaxLength(100); b.Property(x => x.ReasonCode).HasMaxLength(DriverRules.CodeMaximumLength); b.HasIndex(x => new { x.DriverId, x.OccurredAtUtc }).HasDatabaseName("ix_driver_audit_driver_occurred"); }
}

internal sealed class DriverOutboxConfiguration : IEntityTypeConfiguration<DriverOutboxMessage>
{
    public void Configure(EntityTypeBuilder<DriverOutboxMessage> b) { b.ToTable("outbox_messages", DriversPersistence.Schema, t => { t.HasCheckConstraint("ck_driver_outbox_event_type", "char_length(event_type) > 0"); t.HasCheckConstraint("ck_driver_outbox_payload", "jsonb_typeof(payload) = 'object'"); t.HasCheckConstraint("ck_driver_outbox_attempts", "attempt_count >= 0"); }); b.HasKey(x => x.Id).HasName("pk_driver_outbox"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DriverOutboxMessageId(x)); b.Property(x => x.EventType).HasMaxLength(500); b.Property(x => x.Payload).HasColumnType("jsonb"); b.Property(x => x.ErrorCode).HasMaxLength(100); b.HasIndex(x => new { x.ProcessedAtUtc, x.CreatedAtUtc }).HasFilter("processed_at_utc IS NULL").HasDatabaseName("ix_driver_outbox_pending"); }
}
