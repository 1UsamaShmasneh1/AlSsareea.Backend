using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Delivery.Application;
using AlSsareea.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using DeliveryAggregate = AlSsareea.Modules.Delivery.Domain.Delivery;

namespace AlSsareea.Modules.Delivery.Infrastructure.Persistence;

public sealed class DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<DeliveryAggregate> Deliveries => Set<DeliveryAggregate>();
    public DbSet<DeliveryStatusHistory> DeliveryStatusHistory => Set<DeliveryStatusHistory>();
    public DbSet<DeliveryProof> DeliveryProofs => Set<DeliveryProof>();
    internal DbSet<DeliveryOperationIdempotencyRecord> IdempotencyRecords => Set<DeliveryOperationIdempotencyRecord>();
    internal DbSet<DeliveryOutboxMessage> OutboxMessages => Set<DeliveryOutboxMessage>();
    internal DbSet<DeliveryAuditRecord> AuditRecords => Set<DeliveryAuditRecord>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) { EnforceAppendOnly(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { EnforceAppendOnly(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(DeliveryPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryDbContext).Assembly); }

    private void EnforceAppendOnly()
    {
        if (ChangeTracker.Entries<DeliveryStatusHistory>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<DeliveryProof>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<DeliveryOperationIdempotencyRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<DeliveryOutboxMessage>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<DeliveryAuditRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Delivery history, proofs, idempotency, outbox, and audit records are append-only.");
    }
}

public static class DeliveryPersistence
{
    public const string Schema = "delivery";
    public const string MigrationsHistoryTable = "__ef_migrations_history";
    public const string ConnectionStringName = "DeliveryDatabase";
}

internal sealed class DeliveryOperationIdempotencyRecord
{
    private DeliveryOperationIdempotencyRecord() { }
    private DeliveryOperationIdempotencyRecord(Guid id, Guid actorId, string operation, string keyHash, string requestHash, DeliveryId deliveryId, DateTime createdAtUtc) { Id = id; ActorId = actorId; Operation = operation; KeyHash = keyHash; RequestHash = requestHash; DeliveryId = deliveryId; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public DeliveryId DeliveryId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static DeliveryOperationIdempotencyRecord Create(Guid actorId, string operation, string keyHash, string requestHash, DeliveryId deliveryId, DateTime atUtc) => new(Guid.NewGuid(), actorId, operation, keyHash, requestHash, deliveryId, atUtc);
}

internal sealed class DeliveryOutboxMessage
{
    private DeliveryOutboxMessage() { }
    private DeliveryOutboxMessage(Guid id, string eventType, string payload, DateTime occurredAtUtc, DateTime createdAtUtc) { Id = id; EventType = eventType; Payload = payload; OccurredAtUtc = occurredAtUtc; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ErrorCode { get; private set; }
    public static DeliveryOutboxMessage Create(Guid id, string eventType, string payload, DateTime occurredAtUtc, DateTime createdAtUtc) => new(id, eventType, payload, occurredAtUtc, createdAtUtc);
}

internal sealed class DeliveryAuditRecord
{
    private DeliveryAuditRecord() { }
    private DeliveryAuditRecord(Guid id, DeliveryAuditEntry entry) { Id = id; ActorUserId = entry.ActorUserId; DeliveryId = entry.DeliveryId; Operation = entry.Operation; OldStatus = entry.OldStatus; NewStatus = entry.NewStatus; OccurredAtUtc = entry.OccurredAtUtc; CorrelationId = entry.CorrelationId; IdempotencyKeyHash = entry.IdempotencyKeyHash; SafeReasonCode = entry.SafeReasonCode; }
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DeliveryId DeliveryId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public DeliveryStatus OldStatus { get; private set; }
    public DeliveryStatus NewStatus { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? CorrelationId { get; private set; }
    public string IdempotencyKeyHash { get; private set; } = string.Empty;
    public string? SafeReasonCode { get; private set; }
    public static DeliveryAuditRecord Create(DeliveryAuditEntry entry) => new(Guid.NewGuid(), entry);
}
