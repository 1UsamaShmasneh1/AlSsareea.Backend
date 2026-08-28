using AlSsareea.BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Notifications.Infrastructure.Processing;

internal sealed class IntegrationEventDispatcher(IEnumerable<IIntegrationEventConsumer> consumers) : IIntegrationEventDispatcher
{
    public async Task<bool> DispatchAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken)
    {
        IIntegrationEventConsumer[] handlers = [.. consumers.Where(x => x.CanHandle(message.Source, message.EventType))]; if (handlers.Length == 0) return false;
        foreach (IIntegrationEventConsumer handler in handlers) await handler.HandleAsync(message, cancellationToken); return true;
    }
}
internal sealed class IntegrationOutboxWorker(IServiceScopeFactory scopes, IOptions<NotificationProcessingOptions> options, IClock clock, ILogger<IntegrationOutboxWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Guid, string, Exception?> FailureLog = LoggerMessage.Define<string, Guid, string>(LogLevel.Error, new EventId(1703, "IntegrationOutboxMessageFailed"), "Outbox message {Source}/{MessageId} failed with {ErrorCode}.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(Math.Clamp(options.Value.PollingSeconds, 1, 300)));
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOnce(stoppingToken); await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
    private async Task ProcessOnce(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope(); IIntegrationEventDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
        foreach (IIntegrationEventSource source in scope.ServiceProvider.GetServices<IIntegrationEventSource>())
        {
            IReadOnlyList<OutboxMessageEnvelope> pending; try { pending = await source.ReadPendingAsync(Math.Clamp(options.Value.BatchSize, 1, 500), ct); } catch (Exception exception) { FailureLog(logger, source.Source, Guid.Empty, "source_read_failed", exception); continue; }
            foreach (OutboxMessageEnvelope message in pending)
            {
                try { if (await dispatcher.DispatchAsync(message, ct)) await source.MarkProcessedAsync(message.Id, clock.UtcNow, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception exception) { string code = exception.GetType().Name[..Math.Min(120, exception.GetType().Name.Length)]; await source.RecordFailureAsync(message.Id, code, ct); FailureLog(logger, source.Source, message.Id, code, exception); }
            }
        }
    }
}
