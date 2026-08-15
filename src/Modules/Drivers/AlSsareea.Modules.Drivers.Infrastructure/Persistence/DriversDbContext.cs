using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Drivers.Application;
using AlSsareea.Modules.Drivers.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Drivers.Infrastructure.Persistence;

public sealed class DriversDbContext(DbContextOptions<DriversDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
    public DbSet<DriverZoneAssignment> DriverZoneAssignments => Set<DriverZoneAssignment>();
    public DbSet<DriverShift> DriverShifts => Set<DriverShift>();
    public DbSet<DriverViolation> DriverViolations => Set<DriverViolation>();
    public DbSet<DriverSuspension> DriverSuspensions => Set<DriverSuspension>();
    internal DbSet<DriverIdempotencyRecord> IdempotencyRecords => Set<DriverIdempotencyRecord>();
    internal DbSet<DriverAuditRecord> AuditRecords => Set<DriverAuditRecord>();
    internal DbSet<DriverOutboxMessage> OutboxMessages => Set<DriverOutboxMessage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) { EnforceAppendOnly(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { EnforceAppendOnly(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(DriversPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(DriversDbContext).Assembly); }
    private void EnforceAppendOnly()
    {
        if (ChangeTracker.Entries<DriverAuditRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<DriverIdempotencyRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<DriverOutboxMessage>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("Driver audit, idempotency, and outbox records are append-only.");
    }
}

public static class DriversPersistence
{
    public const string Schema = "drivers";
    public const string MigrationsHistoryTable = "__ef_migrations_history";
    public const string ConnectionStringName = "DriversDatabase";
}

internal sealed class DriverIdempotencyRecord
{
    private DriverIdempotencyRecord() { }
    private DriverIdempotencyRecord(DriverIdempotencyEntry entry) { Id = DriverIdempotencyId.New(); ActorUserId = entry.ActorUserId; Operation = entry.Operation; KeyHash = entry.KeyHash; RequestHash = entry.RequestHash; DriverId = entry.DriverId; ResponseStatus = entry.ResponseStatus; ResponseJson = entry.ResponseJson; CreatedAtUtc = entry.CreatedAtUtc; }
    public DriverIdempotencyId Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Operation { get; private set; } = string.Empty; public string KeyHash { get; private set; } = string.Empty; public string RequestHash { get; private set; } = string.Empty; public DriverId DriverId { get; private set; }
    public DriverOperationStatus? ResponseStatus { get; private set; }
    public string? ResponseJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static DriverIdempotencyRecord Create(DriverIdempotencyEntry entry) => new(entry);
}

internal sealed class DriverAuditRecord
{
    private DriverAuditRecord() { }
    private DriverAuditRecord(DriverAuditEntry entry) { Id = DriverAuditId.New(); ActorUserId = entry.ActorUserId; DriverId = entry.DriverId; Action = entry.Action; OccurredAtUtc = entry.OccurredAtUtc; CorrelationId = entry.CorrelationId; ReasonCode = entry.ReasonCode; }
    public DriverAuditId Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DriverId DriverId { get; private set; }
    public string Action { get; private set; } = string.Empty; public DateTime OccurredAtUtc { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? ReasonCode { get; private set; }
    public static DriverAuditRecord Create(DriverAuditEntry entry) => new(entry);
}

internal sealed class DriverOutboxMessage
{
    private DriverOutboxMessage() { }
    private DriverOutboxMessage(DriverOutboxMessageId id, string type, string payload, DateTime occurred, DateTime created) { Id = id; EventType = type; Payload = payload; OccurredAtUtc = occurred; CreatedAtUtc = created; }
    public DriverOutboxMessageId Id { get; private set; }
    public string EventType { get; private set; } = string.Empty; public string Payload { get; private set; } = string.Empty; public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ErrorCode { get; private set; }
    public static DriverOutboxMessage Create(DriverOutboxMessageId id, string type, string payload, DateTime occurred, DateTime created) => new(id, type, payload, occurred, created);
}
