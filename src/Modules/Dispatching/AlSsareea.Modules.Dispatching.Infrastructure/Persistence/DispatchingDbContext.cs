using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Dispatching.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Dispatching.Infrastructure.Persistence;

public sealed class DispatchingDbContext(DbContextOptions<DispatchingDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<DispatchRequest> DispatchRequests => Set<DispatchRequest>();
    public DbSet<DispatchCandidate> DispatchCandidates => Set<DispatchCandidate>();
    public DbSet<DispatchOffer> DispatchOffers => Set<DispatchOffer>();
    public DbSet<DispatchHistory> DispatchHistory => Set<DispatchHistory>();
    internal DbSet<DispatchIdempotencyRecord> IdempotencyRecords => Set<DispatchIdempotencyRecord>();
    internal DbSet<DispatchOutboxMessage> OutboxMessages => Set<DispatchOutboxMessage>();
    internal DbSet<DispatchAuditRecord> AuditRecords => Set<DispatchAuditRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(DispatchingPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispatchingDbContext).Assembly); }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { EnforceAppendOnly(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { EnforceAppendOnly(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    private void EnforceAppendOnly()
    {
        if (ChangeTracker.Entries().Any(x => IsAppendOnly(x.Entity) && x.State is EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("Dispatch candidates, history, idempotency, outbox, and audit are append-only.");
    }
    private static bool IsAppendOnly(object entity) => entity is DispatchCandidate or AlSsareea.Modules.Dispatching.Domain.DispatchHistory or DispatchIdempotencyRecord or DispatchOutboxMessage or DispatchAuditRecord;
}
public static class DispatchingPersistence { public const string Schema = "dispatching"; public const string MigrationsHistoryTable = "__ef_migrations_history"; public const string ConnectionStringName = "DispatchingDatabase"; }

internal sealed class DispatchIdempotencyRecord
{
    private DispatchIdempotencyRecord() { }
    private DispatchIdempotencyRecord(Guid id, Guid actor, string operation, string keyHash, string requestHash, DispatchRequestId requestId, DateTime now) { Id = id; ActorId = actor; Operation = operation; KeyHash = keyHash; RequestHash = requestHash; DispatchRequestId = requestId; CreatedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public string Operation { get; private set; } = string.Empty; public string KeyHash { get; private set; } = string.Empty; public string RequestHash { get; private set; } = string.Empty; public DispatchRequestId DispatchRequestId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    internal static DispatchIdempotencyRecord Create(Guid actor, string operation, string keyHash, string requestHash, DispatchRequestId requestId, DateTime now) => new(Guid.NewGuid(), actor, operation, keyHash, requestHash, requestId, now);
}
internal sealed class DispatchOutboxMessage
{
    private DispatchOutboxMessage() { }
    private DispatchOutboxMessage(Guid id, string eventType, string payload, DateTime occurred, DateTime created) { Id = id; EventType = eventType; Payload = payload; OccurredAtUtc = occurred; CreatedAtUtc = created; }
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty; public string Payload { get; private set; } = string.Empty; public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ErrorCode { get; private set; }
    internal static DispatchOutboxMessage Create(Guid id, string type, string payload, DateTime occurred, DateTime created) => new(id, type, payload, occurred, created);
}
internal sealed class DispatchAuditRecord
{
    private DispatchAuditRecord() { }
    private DispatchAuditRecord(Guid id, Guid actor, DispatchRequestId requestId, string operation, DispatchStatus oldStatus, DispatchStatus newStatus, DateTime occurred, string? correlation, string keyHash, string? reason) { Id = id; ActorUserId = actor; DispatchRequestId = requestId; Operation = operation; OldStatus = oldStatus; NewStatus = newStatus; OccurredAtUtc = occurred; CorrelationId = correlation; IdempotencyKeyHash = keyHash; Reason = reason; }
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DispatchRequestId DispatchRequestId { get; private set; }
    public string Operation { get; private set; } = string.Empty; public DispatchStatus OldStatus { get; private set; }
    public DispatchStatus NewStatus { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? CorrelationId { get; private set; }
    public string IdempotencyKeyHash { get; private set; } = string.Empty; public string? Reason { get; private set; }
    internal static DispatchAuditRecord Create(AlSsareea.Modules.Dispatching.Application.DispatchAuditEntry entry) => new(Guid.NewGuid(), entry.ActorUserId, entry.DispatchRequestId, entry.Operation, entry.OldStatus, entry.NewStatus, entry.OccurredAtUtc, entry.CorrelationId, entry.IdempotencyKeyHash, entry.Reason);
    internal static DispatchAuditRecord Created(Guid actor, DispatchRequest request, string keyHash) => new(Guid.NewGuid(), actor, request.Id, "start", DispatchStatus.Pending, request.Status, request.CreatedAtUtc, null, keyHash, null);
}
