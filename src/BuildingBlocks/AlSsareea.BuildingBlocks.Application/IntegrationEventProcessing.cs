namespace AlSsareea.BuildingBlocks.Application;

public sealed record OutboxMessageEnvelope(string Source, Guid Id, string EventType, string Payload, DateTime OccurredAtUtc, int AttemptCount);
public interface IIntegrationEventSource
{
    string Source { get; }
    Task<IReadOnlyList<OutboxMessageEnvelope>> ReadPendingAsync(int batchSize, CancellationToken cancellationToken);
    Task MarkProcessedAsync(Guid messageId, DateTime processedAtUtc, CancellationToken cancellationToken);
    Task RecordFailureAsync(Guid messageId, string errorCode, CancellationToken cancellationToken);
}
public interface IIntegrationEventConsumer
{
    bool CanHandle(string source, string eventType);
    Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken);
}
public interface IIntegrationEventDispatcher
{
    Task<bool> DispatchAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken);
}
