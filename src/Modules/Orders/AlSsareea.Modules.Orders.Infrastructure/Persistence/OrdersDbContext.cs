using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemOption> OrderItemOptions => Set<OrderItemOption>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    internal DbSet<OrderCreationIdempotencyRecord> IdempotencyRecords => Set<OrderCreationIdempotencyRecord>();
    internal DbSet<OrderOutboxMessage> OutboxMessages => Set<OrderOutboxMessage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) { EnforceAppendOnly(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { EnforceAppendOnly(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(OrdersPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly); }
    private void EnforceAppendOnly()
    {
        if (ChangeTracker.Entries<OrderStatusHistory>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<OrderOutboxMessage>().Any(x => x.State == EntityState.Deleted)) throw new InvalidOperationException("Order history and outbox are append-only.");
    }
}

public static class OrdersPersistence
{
    public const string Schema = "orders";
    public const string MigrationsHistoryTable = "__ef_migrations_history";
    public const string ConnectionStringName = "OrdersDatabase";
}

internal sealed class OrderCreationIdempotencyRecord
{
    private OrderCreationIdempotencyRecord() { }
    private OrderCreationIdempotencyRecord(OrderCreationIdempotencyId id, Guid customerId, string operation, string keyHash, string requestHash, OrderId orderId, DateTime createdAtUtc) { Id = id; CustomerId = customerId; Operation = operation; KeyHash = keyHash; RequestHash = requestHash; OrderId = orderId; CreatedAtUtc = createdAtUtc; }
    public OrderCreationIdempotencyId Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public OrderId OrderId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static OrderCreationIdempotencyRecord Create(Guid customerId, string operation, string keyHash, string requestHash, OrderId orderId, DateTime atUtc) => new(OrderCreationIdempotencyId.New(), customerId, operation, keyHash, requestHash, orderId, atUtc);
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
