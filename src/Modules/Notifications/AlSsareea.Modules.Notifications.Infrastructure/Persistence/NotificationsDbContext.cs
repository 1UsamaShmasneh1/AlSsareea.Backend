using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> Templates => Set<NotificationTemplate>();
    public DbSet<NotificationDelivery> Deliveries => Set<NotificationDelivery>();
    public DbSet<NotificationAttempt> Attempts => Set<NotificationAttempt>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<NotificationPreference> Preferences => Set<NotificationPreference>();
    public DbSet<NotificationInboxMessage> InboxMessages => Set<NotificationInboxMessage>();
    public DbSet<NotificationAuditRecord> AuditRecords => Set<NotificationAuditRecord>();
    public DbSet<NotificationOutboxMessage> OutboxMessages => Set<NotificationOutboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(NotificationsPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly); }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { EnforceHistory(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { EnforceHistory(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    private void EnforceHistory()
    {
        if (ChangeTracker.Entries<NotificationAttempt>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<NotificationInboxMessage>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<NotificationAuditRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<NotificationOutboxMessage>().Any(x => x.State == EntityState.Deleted)) throw new InvalidOperationException("Notification attempts, inbox, audit, and outbox history are append-only.");
    }
}

public static class NotificationsPersistence
{
    public const string Schema = "notifications";
    public const string MigrationsHistoryTable = "__ef_migrations_history";
    public const string ConnectionStringName = "NotificationsDatabase";
}

public sealed class NotificationInboxMessage
{
    private NotificationInboxMessage() { }
    private NotificationInboxMessage(Guid id, string eventType, DateTime occurred, DateTime processed) { Id = id; EventType = eventType; OccurredAtUtc = occurred; ProcessedAtUtc = processed; }
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty; public DateTime OccurredAtUtc { get; private set; }
    public DateTime ProcessedAtUtc { get; private set; }
    public static NotificationInboxMessage Create(Guid id, string eventType, DateTime occurred, DateTime processed) => new(id, eventType, occurred, processed);
}
public sealed class NotificationAuditRecord
{
    private NotificationAuditRecord() { }
    private NotificationAuditRecord(Guid id, Guid userId, string operation, string entityType, Guid entityId, string? detail, DateTime now) { Id = id; UserId = userId; Operation = operation; EntityType = entityType; EntityId = entityId; Detail = detail; OccurredAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Operation { get; private set; } = string.Empty; public string EntityType { get; private set; } = string.Empty; public Guid EntityId { get; private set; }
    public string? Detail { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public static NotificationAuditRecord Create(Guid userId, string operation, string entityType, Guid entityId, string? detail, DateTime now) => new(Guid.NewGuid(), userId, operation, entityType, entityId, detail, now);
}
public sealed class NotificationOutboxMessage
{
    private NotificationOutboxMessage() { }
    private NotificationOutboxMessage(Guid id, string eventType, string payload, DateTime occurred, DateTime created) { Id = id; EventType = eventType; Payload = payload; OccurredAtUtc = occurred; CreatedAtUtc = created; }
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty; public string Payload { get; private set; } = string.Empty; public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ErrorCode { get; private set; }
    public static NotificationOutboxMessage Create(Guid id, string eventType, string payload, DateTime occurred, DateTime created) => new(id, eventType, payload, occurred, created);
}
