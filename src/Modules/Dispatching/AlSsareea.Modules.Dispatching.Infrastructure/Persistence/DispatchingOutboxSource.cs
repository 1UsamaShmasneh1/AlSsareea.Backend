using AlSsareea.BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Dispatching.Infrastructure.Persistence;

internal sealed class DispatchingOutboxSource(DispatchingDbContext db) : IIntegrationEventSource
{
    public string Source => "dispatching";
    public async Task<IReadOnlyList<OutboxMessageEnvelope>> ReadPendingAsync(int batchSize, CancellationToken ct) => await db.OutboxMessages.AsNoTracking().Where(x => x.ProcessedAtUtc == null && x.AttemptCount < 20).OrderBy(x => x.OccurredAtUtc).Take(batchSize).Select(x => new OutboxMessageEnvelope("dispatching", x.Id, x.EventType, x.Payload, x.OccurredAtUtc, x.AttemptCount)).ToArrayAsync(ct);
    public Task MarkProcessedAsync(Guid id, DateTime now, CancellationToken ct) => db.OutboxMessages.Where(x => x.Id == id && x.ProcessedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ProcessedAtUtc, now).SetProperty(x => x.ErrorCode, (string?)null), ct);
    public Task RecordFailureAsync(Guid id, string error, CancellationToken ct) => db.OutboxMessages.Where(x => x.Id == id && x.ProcessedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1).SetProperty(x => x.ErrorCode, error), ct);
}
