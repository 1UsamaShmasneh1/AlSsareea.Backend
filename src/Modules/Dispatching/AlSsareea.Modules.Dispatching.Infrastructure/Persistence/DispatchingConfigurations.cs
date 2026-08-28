using AlSsareea.Modules.Dispatching.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Dispatching.Infrastructure.Persistence;

internal static class DispatchStrongIds { internal static PropertyBuilder<T> StrongId<T>(this PropertyBuilder<T> property, Func<T, Guid> toGuid, Func<Guid, T> fromGuid) => property.HasConversion(x => toGuid(x), x => fromGuid(x)).HasColumnType("uuid").ValueGeneratedNever(); }
internal sealed class DispatchRequestConfiguration : IEntityTypeConfiguration<DispatchRequest>
{
    public void Configure(EntityTypeBuilder<DispatchRequest> b)
    {
        b.ToTable("dispatch_requests", DispatchingPersistence.Schema, t => { t.HasCheckConstraint("ck_dispatch_requests_status", "status BETWEEN 1 AND 6"); t.HasCheckConstraint("ck_dispatch_requests_attempt", "attempt_number >= 0"); t.HasCheckConstraint("ck_dispatch_requests_assignment", "status <> 4 OR assigned_driver_id IS NOT NULL AND completed_at_utc IS NOT NULL"); t.HasCheckConstraint("ck_dispatch_requests_coordinates", "pickup_latitude BETWEEN -90 AND 90 AND pickup_longitude BETWEEN -180 AND 180"); });
        b.HasKey(x => x.Id).HasName("pk_dispatch_requests"); b.Property(x => x.Id).StrongId(x => x.Value, x => new(x)); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.FailureReason).HasMaxLength(DispatchRules.MaximumReasonLength); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.HasIndex(x => x.DeliveryId).IsUnique().HasDatabaseName("ux_dispatch_requests_delivery_id"); b.HasIndex(x => new { x.Status, x.UpdatedAtUtc }).HasDatabaseName("ix_dispatch_requests_status_updated"); b.HasIndex(x => x.AssignedDriverId).HasDatabaseName("ix_dispatch_requests_assigned_driver_id");
        b.HasMany(x => x.Candidates).WithOne().HasForeignKey(x => x.DispatchRequestId).OnDelete(DeleteBehavior.NoAction); b.HasMany(x => x.Offers).WithOne().HasForeignKey(x => x.DispatchRequestId).OnDelete(DeleteBehavior.NoAction); b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.DispatchRequestId).OnDelete(DeleteBehavior.NoAction); b.Navigation(x => x.Candidates).UsePropertyAccessMode(PropertyAccessMode.Field); b.Navigation(x => x.Offers).UsePropertyAccessMode(PropertyAccessMode.Field); b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field); b.Ignore(x => x.DomainEvents);
    }
}
internal sealed class DispatchCandidateConfiguration : IEntityTypeConfiguration<DispatchCandidate>
{
    public void Configure(EntityTypeBuilder<DispatchCandidate> b) { b.ToTable("dispatch_candidates", DispatchingPersistence.Schema, t => { t.HasCheckConstraint("ck_dispatch_candidates_metrics", "distance_meters >= 0 AND eta_seconds >= 0 AND current_load >= 0 AND maximum_capacity > 0"); t.HasCheckConstraint("ck_dispatch_candidates_rank", "rank > 0"); }); b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new(x)); b.Property(x => x.DispatchRequestId).StrongId(x => x.Value, x => new(x)); b.Property(x => x.Score).HasPrecision(10, 4); b.Property(x => x.Explanation).HasMaxLength(DispatchRules.MaximumExplanationLength); b.HasIndex(x => new { x.DispatchRequestId, x.AttemptNumber, x.DriverId }).IsUnique().HasDatabaseName("ux_dispatch_candidates_request_attempt_driver"); b.HasIndex(x => new { x.DispatchRequestId, x.AttemptNumber, x.Rank }).IsUnique().HasDatabaseName("ux_dispatch_candidates_request_attempt_rank"); }
}
internal sealed class DispatchOfferConfiguration : IEntityTypeConfiguration<DispatchOffer>
{
    public void Configure(EntityTypeBuilder<DispatchOffer> b) { b.ToTable("dispatch_offers", DispatchingPersistence.Schema, t => { t.HasCheckConstraint("ck_dispatch_offers_status", "status BETWEEN 1 AND 6"); t.HasCheckConstraint("ck_dispatch_offers_expiry", "expires_at_utc > offered_at_utc"); t.HasCheckConstraint("ck_dispatch_offers_response", "status = 1 AND responded_at_utc IS NULL OR status <> 1 AND responded_at_utc IS NOT NULL"); }); b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new(x)); b.Property(x => x.DispatchRequestId).StrongId(x => x.Value, x => new(x)); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.DeclineReason).HasMaxLength(DispatchRules.MaximumReasonLength); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.HasIndex(x => new { x.DispatchRequestId, x.AttemptNumber, x.DriverId }).IsUnique().HasDatabaseName("ux_dispatch_offers_request_attempt_driver"); b.HasIndex(x => new { x.DispatchRequestId, x.Sequence }).IsUnique().HasDatabaseName("ux_dispatch_offers_request_sequence"); b.HasIndex(x => x.ExpiresAtUtc).HasFilter("status = 1").HasDatabaseName("ix_dispatch_offers_active_expiry"); }
}
internal sealed class DispatchHistoryConfiguration : IEntityTypeConfiguration<DispatchHistory>
{
    public void Configure(EntityTypeBuilder<DispatchHistory> b) { b.ToTable("dispatch_history", DispatchingPersistence.Schema); b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new(x)); b.Property(x => x.DispatchRequestId).StrongId(x => x.Value, x => new(x)); b.Property(x => x.Type).HasConversion<short>(); b.Property(x => x.Detail).HasMaxLength(DispatchRules.MaximumExplanationLength); b.HasIndex(x => new { x.DispatchRequestId, x.OccurredAtUtc }).HasDatabaseName("ix_dispatch_history_request_occurred"); }
}
internal sealed class DispatchIdempotencyConfiguration : IEntityTypeConfiguration<DispatchIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<DispatchIdempotencyRecord> b) { b.ToTable("dispatch_idempotency_records", DispatchingPersistence.Schema, t => t.HasCheckConstraint("ck_dispatch_idempotency_hashes", "char_length(key_hash) = 64 AND char_length(request_hash) = 64")); b.HasKey(x => x.Id); b.Property(x => x.DispatchRequestId).StrongId(x => x.Value, x => new(x)); b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.KeyHash).HasMaxLength(64); b.Property(x => x.RequestHash).HasMaxLength(64); b.HasIndex(x => new { x.ActorId, x.Operation, x.KeyHash }).IsUnique().HasDatabaseName("ux_dispatch_idempotency_actor_operation_key"); }
}
internal sealed class DispatchOutboxConfiguration : IEntityTypeConfiguration<DispatchOutboxMessage>
{
    public void Configure(EntityTypeBuilder<DispatchOutboxMessage> b) { b.ToTable("dispatch_outbox_messages", DispatchingPersistence.Schema, t => { t.HasCheckConstraint("ck_dispatch_outbox_payload", "jsonb_typeof(payload) = 'object'"); t.HasCheckConstraint("ck_dispatch_outbox_attempts", "attempt_count >= 0"); }); b.HasKey(x => x.Id); b.Property(x => x.EventType).HasMaxLength(200); b.Property(x => x.Payload).HasColumnType("jsonb"); b.Property(x => x.ErrorCode).HasMaxLength(200); b.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc }).HasFilter("processed_at_utc IS NULL").HasDatabaseName("ix_dispatch_outbox_pending"); }
}
internal sealed class DispatchAuditConfiguration : IEntityTypeConfiguration<DispatchAuditRecord>
{
    public void Configure(EntityTypeBuilder<DispatchAuditRecord> b) { b.ToTable("dispatch_audit", DispatchingPersistence.Schema, t => t.HasCheckConstraint("ck_dispatch_audit_key_hash", "char_length(idempotency_key_hash) = 64")); b.HasKey(x => x.Id); b.Property(x => x.DispatchRequestId).StrongId(x => x.Value, x => new(x)); b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.OldStatus).HasConversion<short>(); b.Property(x => x.NewStatus).HasConversion<short>(); b.Property(x => x.CorrelationId).HasMaxLength(100); b.Property(x => x.IdempotencyKeyHash).HasMaxLength(64); b.Property(x => x.Reason).HasMaxLength(DispatchRules.MaximumReasonLength); b.HasIndex(x => new { x.DispatchRequestId, x.OccurredAtUtc }).HasDatabaseName("ix_dispatch_audit_request_occurred"); }
}
