using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Dispatching.Application;
using AlSsareea.Modules.Dispatching.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Dispatching.Infrastructure.Persistence;

internal sealed class DispatchRepository(DispatchingDbContext db, IClock clock) : IDispatchRepository
{
    public Task<DispatchRequest?> GetAsync(DispatchRequestId id, bool noTracking, CancellationToken ct) => Aggregate(noTracking).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<DispatchRequest?> GetByDeliveryAsync(Guid deliveryId, bool noTracking, CancellationToken ct) => Aggregate(noTracking).SingleOrDefaultAsync(x => x.DeliveryId == deliveryId, ct);
    public Task<DispatchIdempotencyResult?> FindIdempotencyAsync(Guid actorId, string operation, string keyHash, CancellationToken ct) => db.IdempotencyRecords.AsNoTracking().Where(x => x.ActorId == actorId && x.Operation == operation && x.KeyHash == keyHash).Select(x => new DispatchIdempotencyResult(x.DispatchRequestId.Value, x.RequestHash)).SingleOrDefaultAsync(ct);
    public async Task<bool> CreateAsync(DispatchRequest request, Guid actorId, string keyHash, string requestHash, IReadOnlyCollection<IIntegrationEvent> events, CancellationToken ct)
    {
        db.DispatchRequests.Add(request); db.IdempotencyRecords.Add(DispatchIdempotencyRecord.Create(actorId, "start", keyHash, requestHash, request.Id, clock.UtcNow)); db.AuditRecords.Add(DispatchAuditRecord.Created(actorId, request, keyHash)); AddOutbox(events);
        return await Save(request, ct);
    }
    public async Task<bool> SaveAsync(DispatchRequest request, Guid actorId, string operation, string keyHash, string requestHash, DispatchAuditEntry audit, IReadOnlyCollection<IIntegrationEvent> events, CancellationToken ct)
    {
        db.IdempotencyRecords.Add(DispatchIdempotencyRecord.Create(actorId, operation, keyHash, requestHash, request.Id, clock.UtcNow)); db.AuditRecords.Add(DispatchAuditRecord.Create(audit)); AddOutbox(events); return await Save(request, ct);
    }
    public async Task<IReadOnlyList<DispatchRequest>> GetExpiredAsync(DateTime now, int count, CancellationToken ct)
    {
        DispatchRequestId[] ids = await db.DispatchOffers.AsNoTracking().Where(x => x.Status == DispatchOfferStatus.Pending && x.ExpiresAtUtc <= now).OrderBy(x => x.ExpiresAtUtc).Select(x => x.DispatchRequestId).Take(count).ToArrayAsync(ct); return await Aggregate(false).Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
    }
    private IQueryable<DispatchRequest> Aggregate(bool noTracking) { IQueryable<DispatchRequest> query = db.DispatchRequests.Include(x => x.Candidates).Include(x => x.Offers).Include(x => x.History).AsSplitQuery(); return noTracking ? query.AsNoTracking() : query; }
    private void AddOutbox(IEnumerable<IIntegrationEvent> events) { DateTime created = clock.UtcNow; foreach (IIntegrationEvent item in events) db.OutboxMessages.Add(DispatchOutboxMessage.Create(item.Id, item.GetType().FullName ?? item.GetType().Name, JsonSerializer.Serialize(item, item.GetType()), item.OccurredAtUtc, created)); }
    private async Task<bool> Save(DispatchRequest request, CancellationToken ct) { try { await db.SaveChangesAsync(ct); request.ClearDomainEvents(); return true; } catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; } }
}
