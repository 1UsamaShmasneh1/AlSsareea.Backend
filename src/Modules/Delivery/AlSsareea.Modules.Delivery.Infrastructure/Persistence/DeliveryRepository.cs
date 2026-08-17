using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Delivery.Application;
using AlSsareea.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using DeliveryAggregate = AlSsareea.Modules.Delivery.Domain.Delivery;

namespace AlSsareea.Modules.Delivery.Infrastructure.Persistence;

internal sealed class DeliveryRepository(DeliveryDbContext db, IClock clock) : IDeliveryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DeliveryStatus[] ActiveStatuses = [DeliveryStatus.Created, DeliveryStatus.Assigned, DeliveryStatus.HeadingToPickup, DeliveryStatus.ArrivedAtPickup, DeliveryStatus.PickedUp, DeliveryStatus.InTransit, DeliveryStatus.ArrivedAtDropOff];

    public Task<DeliveryAggregate?> GetAsync(DeliveryId id, bool noTracking, CancellationToken cancellationToken) => Aggregate(noTracking).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<DeliveryAggregate?> GetByOrderAsync(Guid orderId, bool noTracking, CancellationToken cancellationToken) => Aggregate(noTracking).SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
    public Task<DeliveryAggregate?> GetCurrentForCustomerAsync(Guid customerId, CancellationToken cancellationToken) => Aggregate(true).Where(x => x.CustomerUserId == customerId && ActiveStatuses.Contains(x.Status)).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
    public Task<DeliveryAggregate?> GetCurrentForDriverAsync(Guid driverId, CancellationToken cancellationToken) => Aggregate(true).Where(x => x.DriverId == driverId && ActiveStatuses.Contains(x.Status)).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);

    public Task<DeliveryIdempotencyResult?> FindIdempotencyAsync(Guid actorId, string operation, string keyHash, CancellationToken cancellationToken) =>
        db.IdempotencyRecords.AsNoTracking().Where(x => x.ActorId == actorId && x.Operation == operation && x.KeyHash == keyHash).Select(x => new DeliveryIdempotencyResult(x.DeliveryId.Value, x.RequestHash)).SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> CreateAsync(DeliveryAggregate delivery, Guid actorId, string keyHash, string requestHash, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken)
    {
        db.Deliveries.Add(delivery);
        db.IdempotencyRecords.Add(DeliveryOperationIdempotencyRecord.Create(actorId, "create", keyHash, requestHash, delivery.Id, clock.UtcNow));
        AddOutbox(integrationEvents);
        try { await db.SaveChangesAsync(cancellationToken); delivery.ClearDomainEvents(); return true; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; }
    }

    public async Task<bool> SaveOperationAsync(DeliveryAggregate delivery, Guid actorId, string operation, string keyHash, string requestHash, DeliveryAuditEntry audit, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken)
    {
        db.IdempotencyRecords.Add(DeliveryOperationIdempotencyRecord.Create(actorId, operation, keyHash, requestHash, delivery.Id, clock.UtcNow));
        db.AuditRecords.Add(DeliveryAuditRecord.Create(audit));
        AddOutbox(integrationEvents);
        try { await db.SaveChangesAsync(cancellationToken); delivery.ClearDomainEvents(); return true; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; }
    }

    private IQueryable<DeliveryAggregate> Aggregate(bool noTracking)
    {
        IQueryable<DeliveryAggregate> query = db.Deliveries.Include(x => x.StatusHistory).Include(x => x.Proofs).AsSplitQuery();
        return noTracking ? query.AsNoTracking() : query;
    }

    private void AddOutbox(IEnumerable<IIntegrationEvent> events)
    {
        DateTime now = clock.UtcNow;
        foreach (IIntegrationEvent integrationEvent in events)
            db.OutboxMessages.Add(DeliveryOutboxMessage.Create(integrationEvent.Id, integrationEvent.GetType().Name, JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions), integrationEvent.OccurredAtUtc, now));
    }
}
