using AlSsareea.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DeliveryAggregate = AlSsareea.Modules.Delivery.Domain.Delivery;

namespace AlSsareea.Modules.Delivery.Infrastructure.Persistence;

internal static class DeliveryConfigurationExtensions
{
    internal static PropertyBuilder<T> StrongId<T>(this PropertyBuilder<T> property, Func<T, Guid> toGuid, Func<Guid, T> fromGuid) => property.HasConversion(value => toGuid(value), value => fromGuid(value)).HasColumnType("uuid").ValueGeneratedNever();
}

internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<DeliveryAggregate>
{
    public void Configure(EntityTypeBuilder<DeliveryAggregate> b)
    {
        b.ToTable("deliveries", DeliveryPersistence.Schema, t =>
        {
            t.HasCheckConstraint("ck_deliveries_status", "status BETWEEN 1 AND 10");
            t.HasCheckConstraint("ck_deliveries_proof_requirements", "proof_requirements BETWEEN 0 AND 15");
            t.HasCheckConstraint("ck_deliveries_pin_configuration", "(proof_requirements & 1) = 0 OR pin_hash IS NOT NULL AND pin_salt IS NOT NULL");
            t.HasCheckConstraint("ck_deliveries_pin_attempts", "pin_failed_attempts BETWEEN 0 AND 5");
            t.HasCheckConstraint("ck_deliveries_driver_assignment", "driver_id IS NULL OR assigned_at_utc IS NOT NULL");
            t.HasCheckConstraint("ck_deliveries_terminal_timestamps", "(status <> 8 OR delivered_at_utc IS NOT NULL) AND (status <> 9 OR failed_at_utc IS NOT NULL) AND (status <> 10 OR cancelled_at_utc IS NOT NULL)");
        });
        b.HasKey(x => x.Id).HasName("pk_deliveries");
        b.Property(x => x.Id).StrongId(x => x.Value, x => new DeliveryId(x));
        b.Property(x => x.Status).HasConversion<short>();
        b.Property(x => x.ProofRequirements).HasConversion<short>();
        b.Property(x => x.FailureReason).HasConversion<short>();
        b.Property(x => x.PinHash).HasMaxLength(128);
        b.Property(x => x.PinSalt).HasMaxLength(128);
        b.Property(x => x.FailureNotes).HasMaxLength(DeliveryRules.FailureNotesMaximumLength);
        b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        b.HasIndex(x => x.OrderId).IsUnique().HasDatabaseName("ux_deliveries_order_id");
        b.HasIndex(x => x.DriverId).HasDatabaseName("ix_deliveries_driver_id");
        b.HasIndex(x => x.CustomerId).HasDatabaseName("ix_deliveries_customer_id");
        b.HasIndex(x => x.CustomerUserId).HasDatabaseName("ix_deliveries_customer_user_id");
        b.HasIndex(x => x.MerchantId).HasDatabaseName("ix_deliveries_merchant_id");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_deliveries_status");
        b.HasIndex(x => new { x.DriverId, x.Status }).HasDatabaseName("ix_deliveries_driver_status");
        b.HasIndex(x => new { x.CustomerId, x.Status }).HasDatabaseName("ix_deliveries_customer_status");
        b.OwnsOne(x => x.Pickup, owned =>
        {
            owned.Property(x => x.MerchantId).HasColumnName("pickup_merchant_id"); owned.Property(x => x.BranchId).HasColumnName("pickup_branch_id");
            owned.Property(x => x.Address).HasColumnName("pickup_address").HasMaxLength(DeliveryRules.AddressMaximumLength);
            owned.Property(x => x.ContactName).HasColumnName("pickup_contact_name").HasMaxLength(DeliveryRules.ContactMaximumLength);
            owned.Property(x => x.PhoneNumber).HasColumnName("pickup_phone_number").HasMaxLength(DeliveryRules.ContactMaximumLength);
            owned.Property(x => x.Instructions).HasColumnName("pickup_instructions").HasMaxLength(DeliveryRules.InstructionsMaximumLength);
            owned.Property(x => x.Latitude).HasColumnName("pickup_latitude"); owned.Property(x => x.Longitude).HasColumnName("pickup_longitude");
        });
        b.OwnsOne(x => x.DropOff, owned =>
        {
            owned.Property(x => x.AddressId).HasColumnName("drop_off_address_id");
            owned.Property(x => x.Address).HasColumnName("drop_off_address").HasMaxLength(DeliveryRules.AddressMaximumLength);
            owned.Property(x => x.RecipientName).HasColumnName("drop_off_recipient_name").HasMaxLength(DeliveryRules.RecipientNameMaximumLength);
            owned.Property(x => x.PhoneNumber).HasColumnName("drop_off_phone_number").HasMaxLength(DeliveryRules.ContactMaximumLength);
            owned.Property(x => x.Floor).HasColumnName("drop_off_floor").HasMaxLength(DeliveryRules.ContactMaximumLength);
            owned.Property(x => x.Instructions).HasColumnName("drop_off_instructions").HasMaxLength(DeliveryRules.InstructionsMaximumLength);
            owned.Property(x => x.Latitude).HasColumnName("drop_off_latitude"); owned.Property(x => x.Longitude).HasColumnName("drop_off_longitude");
        });
        b.HasMany(x => x.StatusHistory).WithOne().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_delivery_status_history_deliveries_delivery_id");
        b.HasMany(x => x.Proofs).WithOne().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_delivery_proofs_deliveries_delivery_id");
        b.Navigation(x => x.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Proofs).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.DomainEvents); b.Ignore(x => x.IsTerminal); b.Ignore(x => x.IsCustomerTrackingVisible);
    }
}

internal sealed class DeliveryStatusHistoryConfiguration : IEntityTypeConfiguration<DeliveryStatusHistory>
{
    public void Configure(EntityTypeBuilder<DeliveryStatusHistory> b)
    {
        b.ToTable("delivery_status_history", DeliveryPersistence.Schema, t => t.HasCheckConstraint("ck_delivery_status_history_changed", "previous_status IS NULL OR previous_status <> new_status"));
        b.HasKey(x => x.Id).HasName("pk_delivery_status_history"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DeliveryStatusHistoryId(x)); b.Property(x => x.DeliveryId).StrongId(x => x.Value, x => new DeliveryId(x));
        b.Property(x => x.PreviousStatus).HasConversion<short>(); b.Property(x => x.NewStatus).HasConversion<short>(); b.Property(x => x.Source).HasConversion<short>();
        b.Property(x => x.ReasonCode).HasMaxLength(80); b.Property(x => x.ReasonText).HasMaxLength(DeliveryRules.FailureNotesMaximumLength);
        b.HasIndex(x => new { x.DeliveryId, x.ChangedAtUtc }).HasDatabaseName("ix_delivery_status_history_delivery_changed");
    }
}

internal sealed class DeliveryProofConfiguration : IEntityTypeConfiguration<DeliveryProof>
{
    public void Configure(EntityTypeBuilder<DeliveryProof> b)
    {
        b.ToTable("delivery_proofs", DeliveryPersistence.Schema, t => t.HasCheckConstraint("ck_delivery_proofs_content", "(type IN (2,3) AND media_asset_id IS NOT NULL AND recipient_name IS NULL) OR (type = 4 AND media_asset_id IS NULL AND recipient_name IS NOT NULL) OR (type = 1 AND media_asset_id IS NULL AND recipient_name IS NULL)"));
        b.HasKey(x => x.Id).HasName("pk_delivery_proofs"); b.Property(x => x.Id).StrongId(x => x.Value, x => new DeliveryProofId(x)); b.Property(x => x.DeliveryId).StrongId(x => x.Value, x => new DeliveryId(x)); b.Property(x => x.Type).HasConversion<short>(); b.Property(x => x.RecipientName).HasMaxLength(DeliveryRules.RecipientNameMaximumLength);
        b.HasIndex(x => new { x.DeliveryId, x.Type }).IsUnique().HasDatabaseName("ux_delivery_proofs_delivery_type"); b.HasIndex(x => x.MediaAssetId).HasDatabaseName("ix_delivery_proofs_media_asset_id");
    }
}

internal sealed class DeliveryIdempotencyConfiguration : IEntityTypeConfiguration<DeliveryOperationIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<DeliveryOperationIdempotencyRecord> b)
    {
        b.ToTable("delivery_operation_idempotency", DeliveryPersistence.Schema, t => t.HasCheckConstraint("ck_delivery_idempotency_hashes", "char_length(key_hash) = 64 AND char_length(request_hash) = 64")); b.HasKey(x => x.Id).HasName("pk_delivery_operation_idempotency"); b.Property(x => x.DeliveryId).StrongId(x => x.Value, x => new DeliveryId(x)); b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.KeyHash).HasMaxLength(64); b.Property(x => x.RequestHash).HasMaxLength(64); b.HasIndex(x => new { x.ActorId, x.Operation, x.KeyHash }).IsUnique().HasDatabaseName("ux_delivery_idempotency_actor_operation_key"); b.HasIndex(x => x.DeliveryId).HasDatabaseName("ix_delivery_idempotency_delivery_id"); b.HasOne<DeliveryAggregate>().WithMany().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_delivery_idempotency_deliveries_delivery_id");
    }
}

internal sealed class DeliveryOutboxConfiguration : IEntityTypeConfiguration<DeliveryOutboxMessage>
{
    public void Configure(EntityTypeBuilder<DeliveryOutboxMessage> b)
    {
        b.ToTable("outbox_messages", DeliveryPersistence.Schema, t => { t.HasCheckConstraint("ck_delivery_outbox_payload", "jsonb_typeof(payload) = 'object'"); t.HasCheckConstraint("ck_delivery_outbox_attempts", "attempt_count >= 0"); }); b.HasKey(x => x.Id).HasName("pk_delivery_outbox_messages"); b.Property(x => x.EventType).HasMaxLength(200); b.Property(x => x.Payload).HasColumnType("jsonb"); b.Property(x => x.ErrorCode).HasMaxLength(200); b.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc }).HasFilter("processed_at_utc IS NULL").HasDatabaseName("ix_delivery_outbox_pending");
    }
}

internal sealed class DeliveryAuditConfiguration : IEntityTypeConfiguration<DeliveryAuditRecord>
{
    public void Configure(EntityTypeBuilder<DeliveryAuditRecord> b)
    {
        b.ToTable("delivery_audit", DeliveryPersistence.Schema, t => t.HasCheckConstraint("ck_delivery_audit_key_hash", "char_length(idempotency_key_hash) = 64")); b.HasKey(x => x.Id).HasName("pk_delivery_audit"); b.Property(x => x.DeliveryId).StrongId(x => x.Value, x => new DeliveryId(x)); b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.OldStatus).HasConversion<short>(); b.Property(x => x.NewStatus).HasConversion<short>(); b.Property(x => x.CorrelationId).HasMaxLength(100); b.Property(x => x.IdempotencyKeyHash).HasMaxLength(64); b.Property(x => x.SafeReasonCode).HasMaxLength(80); b.HasIndex(x => new { x.DeliveryId, x.OccurredAtUtc }).HasDatabaseName("ix_delivery_audit_delivery_occurred"); b.HasOne<DeliveryAggregate>().WithMany().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_delivery_audit_deliveries_delivery_id");
    }
}
