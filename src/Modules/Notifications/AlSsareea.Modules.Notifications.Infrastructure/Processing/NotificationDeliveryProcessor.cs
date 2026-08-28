using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Domain;
using AlSsareea.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Notifications.Infrastructure.Processing;

public sealed class NotificationProcessingOptions
{
    public const string SectionName = "Notifications:Processing";
    public int PollingSeconds { get; set; } = 5; public int BatchSize { get; set; } = 50; public int DeliveryLimitPerMinute { get; set; } = 120; public int MaximumBackoffSeconds { get; set; } = 900; public int ProcessingLeaseSeconds { get; set; } = 300;
}
internal interface INotificationDeliveryProcessor { Task<int> ProcessBatchAsync(CancellationToken cancellationToken); }
internal sealed class NotificationDeliveryProcessor(NotificationsDbContext db, IEnumerable<INotificationChannelSender> senders, ITokenProtector tokenProtector, IClock clock, IOptions<NotificationProcessingOptions> options, ILogger<NotificationDeliveryProcessor> logger) : INotificationDeliveryProcessor
{
    private static readonly Action<ILogger, int, Exception?> ProcessedLog = LoggerMessage.Define<int>(LogLevel.Information, new EventId(1701, "NotificationDeliveriesProcessed"), "Processed {Count} notification deliveries.");
    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        DateTime now = clock.UtcNow; int batch = Math.Clamp(options.Value.BatchSize, 1, 500); DateTime expiredLease = now.AddSeconds(-options.Value.ProcessingLeaseSeconds);
        await db.Deliveries.Where(x => x.Status == NotificationStatus.Processing && x.UpdatedAtUtc <= expiredLease).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NotificationStatus.RetryScheduled).SetProperty(x => x.NextAttemptAtUtc, now).SetProperty(x => x.LastErrorCode, "notifications.delivery.processing_lease_expired").SetProperty(x => x.UpdatedAtUtc, now).SetProperty(x => x.ConcurrencyStamp, Guid.NewGuid()), ct);
        NotificationDeliveryId[] ids = await db.Deliveries.AsNoTracking().Where(x => (x.Status == NotificationStatus.Queued || x.Status == NotificationStatus.RetryScheduled) && x.NextAttemptAtUtc <= now).OrderBy(x => x.NextAttemptAtUtc).Select(x => x.Id).Take(batch).ToArrayAsync(ct); int processed = 0;
        foreach (NotificationDeliveryId id in ids)
        {
            int claimed = await db.Deliveries.Where(x => x.Id == id && (x.Status == NotificationStatus.Queued || x.Status == NotificationStatus.RetryScheduled) && x.NextAttemptAtUtc <= now).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NotificationStatus.Processing).SetProperty(x => x.NextAttemptAtUtc, (DateTime?)null).SetProperty(x => x.UpdatedAtUtc, now).SetProperty(x => x.ConcurrencyStamp, Guid.NewGuid()), ct); if (claimed == 0) continue;
            db.ChangeTracker.Clear(); NotificationDelivery delivery = await db.Deliveries.Include(x => x.Attempts).SingleAsync(x => x.Id == id, ct); Notification notification = await db.Notifications.SingleAsync(x => x.Id == delivery.NotificationId, ct); DeviceToken? device = delivery.DeviceTokenId is null ? null : await db.DeviceTokens.SingleOrDefaultAsync(x => x.Id == delivery.DeviceTokenId, ct);
            string? token = device is { IsActive: true } ? tokenProtector.Unprotect(device.TokenCiphertext) : null; INotificationChannelSender? sender = senders.SingleOrDefault(x => x.Provider == delivery.Provider && x.Channel == notification.Channel);
            int recentAttempts = await (from attempt in db.Attempts.AsNoTracking() join candidate in db.Deliveries.AsNoTracking() on attempt.NotificationDeliveryId equals candidate.Id join ownerNotification in db.Notifications.AsNoTracking() on candidate.NotificationId equals ownerNotification.Id where ownerNotification.UserId == notification.UserId && attempt.AttemptedAtUtc >= now.AddMinutes(-1) select attempt.Id).CountAsync(ct);
            ProviderSendResult result = delivery.DeviceTokenId is not null && device is not { IsActive: true } ? new(false, false, ProviderFailureKind.InvalidToken, "notifications.device_token.inactive") : recentAttempts >= Math.Max(1, options.Value.DeliveryLimitPerMinute) ? new(false, false, ProviderFailureKind.RateLimited, "notifications.delivery.rate_limited") : sender is null ? new(false, false, ProviderFailureKind.NotConfigured, "notifications.provider.not_configured") : await sender.SendAsync(new(delivery.Id, notification.Channel, delivery.Provider, token, notification.Subject, notification.Body), ct); int previousAttemptCount = delivery.AttemptCount;
            bool invalidated = false; if (result.Delivered) delivery.RecordDelivered(result.ProviderMessageId, now); else if (result.Accepted) delivery.RecordAccepted(result.ProviderMessageId, now); else { TimeSpan delay = TimeSpan.FromSeconds(Math.Min(options.Value.MaximumBackoffSeconds, 5 * Math.Pow(2, delivery.AttemptCount))); delivery.RecordFailure(result.FailureKind, result.ErrorCode ?? "notifications.provider.failure", now, delay); if (result.FailureKind == ProviderFailureKind.InvalidToken && device is not null) invalidated = device.Deactivate("provider_invalid", now); }
            NotificationAttempt recordedAttempt = delivery.Attempts.Single(x => x.AttemptNumber == previousAttemptCount + 1); db.Entry(recordedAttempt).State = EntityState.Added; notification.SynchronizeStatus(delivery.Status, now); string operation = delivery.Status switch { NotificationStatus.ProviderAccepted or NotificationStatus.Delivered => "delivery_accepted", NotificationStatus.RetryScheduled => "delivery_retry_scheduled", _ => "delivery_failed" }; string detail = $"provider={delivery.Provider};error={result.ErrorCode ?? "none"}"; db.AuditRecords.Add(NotificationAuditRecord.Create(notification.UserId, operation, "delivery", delivery.Id.Value, detail, now)); if (invalidated && device is not null) db.AuditRecords.Add(NotificationAuditRecord.Create(notification.UserId, "invalidate_device", "device_token", device.Id.Value, $"provider={delivery.Provider};error={result.ErrorCode ?? "invalid_token"}", now)); await db.SaveChangesAsync(ct); processed++;
        }
        if (processed > 0) ProcessedLog(logger, processed, null); return processed;
    }
}
internal sealed class NotificationDeliveryWorker(IServiceScopeFactory scopes, IOptions<NotificationProcessingOptions> options, ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> BatchFailedLog = LoggerMessage.Define(LogLevel.Error, new EventId(1702, "NotificationDeliveryBatchFailed"), "Notification delivery batch failed.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollingSeconds, 1, 300)); using PeriodicTimer timer = new(interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await using AsyncServiceScope scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<INotificationDeliveryProcessor>().ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { BatchFailedLog(logger, exception); }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
