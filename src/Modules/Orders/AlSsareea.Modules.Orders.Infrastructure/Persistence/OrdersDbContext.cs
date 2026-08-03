using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemOption> OrderItemOptions => Set<OrderItemOption>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    internal DbSet<OrderOperationIdempotencyRecord> IdempotencyRecords => Set<OrderOperationIdempotencyRecord>();
    internal DbSet<OrderOutboxMessage> OutboxMessages => Set<OrderOutboxMessage>();
    internal DbSet<MerchantOrderAuditRecord> MerchantOrderAudit => Set<MerchantOrderAuditRecord>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) { EnforceAppendOnly(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { EnforceAppendOnly(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(OrdersPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly); }
    private void EnforceAppendOnly()
    {
        if (ChangeTracker.Entries<OrderStatusHistory>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<OrderOutboxMessage>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<OrderOperationIdempotencyRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<MerchantOrderAuditRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Order history, idempotency, outbox, and audit records are append-only.");
    }
}

public static class OrdersPersistence
{
    public const string Schema = "orders";
    public const string MigrationsHistoryTable = "__ef_migrations_history";
    public const string ConnectionStringName = "OrdersDatabase";
}

internal sealed class OrderOperationIdempotencyRecord
{
    private OrderOperationIdempotencyRecord() { }
    private OrderOperationIdempotencyRecord(OrderCreationIdempotencyId id, Guid actorId, string operation, string keyHash, string requestHash, OrderId orderId, DateTime createdAtUtc) { Id = id; ActorId = actorId; Operation = operation; KeyHash = keyHash; RequestHash = requestHash; OrderId = orderId; CreatedAtUtc = createdAtUtc; }
    public OrderCreationIdempotencyId Id { get; private set; }
    public Guid ActorId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public OrderId OrderId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static OrderOperationIdempotencyRecord Create(Guid actorId, string operation, string keyHash, string requestHash, OrderId orderId, DateTime atUtc) => new(OrderCreationIdempotencyId.New(), actorId, operation, keyHash, requestHash, orderId, atUtc);
}

internal sealed class MerchantOrderAuditRecord
{
    private MerchantOrderAuditRecord() { }
    private MerchantOrderAuditRecord(MerchantOrderAuditId id, MerchantOrderAuditEntry entry)
    {
        Id = id; ActorUserId = entry.ActorUserId; MerchantId = entry.MerchantId; BranchId = entry.BranchId;
        OrderId = new OrderId(entry.OrderId); Operation = entry.Operation; OldStatus = entry.OldStatus;
        NewStatus = entry.NewStatus; OccurredAtUtc = entry.OccurredAtUtc; CorrelationId = entry.CorrelationId;
        IdempotencyKeyHash = entry.IdempotencyKeyHash; SafeReasonCode = entry.SafeReasonCode;
    }
    public MerchantOrderAuditId Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid? BranchId { get; private set; }
    public OrderId OrderId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public OrderStatus OldStatus { get; private set; }
    public OrderStatus NewStatus { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? CorrelationId { get; private set; }
    public string IdempotencyKeyHash { get; private set; } = string.Empty;
    public string? SafeReasonCode { get; private set; }
    public static MerchantOrderAuditRecord Create(MerchantOrderAuditEntry entry) => new(MerchantOrderAuditId.New(), entry);
}

internal sealed class OrderOutboxMessage
{
    private OrderOutboxMessage() { }
    private OrderOutboxMessage(OrderOutboxMessageId id, string eventType, string payload, DateTime occurredAtUtc, DateTime createdAtUtc) { Id = id; EventType = eventType; Payload = payload; OccurredAtUtc = occurredAtUtc; CreatedAtUtc = createdAtUtc; }
    public OrderOutboxMessageId Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ErrorCode { get; private set; }
    public static OrderOutboxMessage Create(OrderOutboxMessageId id, string eventType, string payload, DateTime occurredAtUtc, DateTime createdAtUtc) => new(id, eventType, payload, occurredAtUtc, createdAtUtc);
}
