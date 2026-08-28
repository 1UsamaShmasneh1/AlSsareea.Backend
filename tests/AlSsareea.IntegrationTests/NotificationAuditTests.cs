using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Contracts;
using AlSsareea.Modules.Notifications.Domain;
using AlSsareea.Modules.Notifications.Infrastructure.Persistence;
using AlSsareea.Modules.Notifications.Infrastructure.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class NotificationAuditTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SelfServiceAndEventRuntimePathsWriteSafeAuditRecords()
    {
        DateTime now = DateTime.UtcNow.Date.AddDays(2).AddHours(12); Guid userId = Guid.NewGuid(); await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); NotificationsDbContext db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(); INotificationStore store = scope.ServiceProvider.GetRequiredService<INotificationStore>(); NotificationService service = new(store, scope.ServiceProvider.GetRequiredService<ITemplateRenderer>(), scope.ServiceProvider.GetRequiredService<ITokenProtector>(), new FixedClock(now));
        NotificationOperationResult<DeviceTokenResponse> registration = await service.RegisterDeviceAsync(userId, new("audit-device-token-" + Guid.NewGuid().ToString("N"), (short)PushPlatform.Android, (short)PushProvider.Fcm)); Guid tokenId = registration.Value!.Id; await service.UnregisterDeviceAsync(userId, tokenId); await service.UpdatePreferencesAsync(userId, new([new("audit-category", (short)NotificationChannel.Push, false)]));
        Notification first = InApp(userId, "audit-read", now); first.QueueDelivery(null, "inapp", 1, now); Notification second = InApp(userId, "audit-read-all", now); second.QueueDelivery(null, "inapp", 1, now); db.Notifications.AddRange(first, second); await db.SaveChangesAsync(); await service.MarkReadAsync(userId, first.Id.Value); Assert.Equal(1, await service.MarkAllReadAsync(userId));
        Guid eventId = Guid.NewGuid(); Assert.True(await service.ConsumeAsync("OrderCreatedIntegrationEvent", new(eventId, userId, "en", "order_updates", "order.created.customer", new Dictionary<string, string> { ["orderNumber"] = "AUD-17" }, [NotificationChannel.InApp], now)));
        db.ChangeTracker.Clear(); NotificationAuditRecord[] records = await db.AuditRecords.AsNoTracking().Where(x => x.UserId == userId).ToArrayAsync();
        AssertAudit(records, "register_device", "device_token", tokenId, now); AssertAudit(records, "unregister_device", "device_token", tokenId, now); AssertAudit(records, "update_preferences", "recipient_preferences", userId, now); AssertAudit(records, "mark_read", "notification", first.Id.Value, now); NotificationAuditRecord readAll = AssertAudit(records, "mark_all_read", "recipient_notifications", userId, now); Assert.Equal("count=1", readAll.Detail); NotificationAuditRecord consumed = AssertAudit(records, "consume_event", "inbox_message", eventId, now); Assert.Equal("OrderCreatedIntegrationEvent", consumed.Detail); Assert.DoesNotContain(records, x => x.Detail?.Contains("audit-device-token", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("accepted", NotificationStatus.ProviderAccepted, "delivery_accepted")]
    [InlineData("transient", NotificationStatus.RetryScheduled, "delivery_retry_scheduled")]
    [InlineData("permanent", NotificationStatus.Failed, "delivery_failed")]
    public async Task DeliveryProcessorWritesDistinctTransitionAudit(string outcome, NotificationStatus expectedStatus, string expectedOperation)
    {
        DateTime now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc); await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); (NotificationsDbContext db, DeviceToken token, NotificationDelivery delivery) = await SeedPush(scope, now); ProviderSendResult result = outcome switch { "accepted" => new(true, false, ProviderFailureKind.None, ProviderMessageId: "provider-message"), "transient" => new(false, false, ProviderFailureKind.Transient, "provider.temporary"), _ => new(false, false, ProviderFailureKind.Permanent, "provider.rejected") }; RecordingSender sender = new(result); NotificationDeliveryProcessor processor = Processor(scope, db, sender, now);
        await processor.ProcessBatchAsync(default); db.ChangeTracker.Clear(); NotificationDelivery persisted = await db.Deliveries.AsNoTracking().SingleAsync(x => x.Id == delivery.Id); Assert.Equal(expectedStatus, persisted.Status); NotificationAuditRecord audit = await db.AuditRecords.AsNoTracking().SingleAsync(x => x.EntityId == delivery.Id.Value && x.Operation == expectedOperation); Assert.Equal(token.UserId, audit.UserId); Assert.Equal("delivery", audit.EntityType); Assert.Equal(now, audit.OccurredAtUtc); Assert.Contains("provider=fcm", audit.Detail, StringComparison.Ordinal); Assert.DoesNotContain(token.TokenCiphertext, audit.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidTokenResultDeactivatesAndAuditsTokenAndSkipsLaterProviderCall()
    {
        DateTime now = new(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc); await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); (NotificationsDbContext db, DeviceToken token, NotificationDelivery firstDelivery) = await SeedPush(scope, now); RecordingSender sender = new(new(false, false, ProviderFailureKind.InvalidToken, "provider.unregistered")); NotificationDeliveryProcessor processor = Processor(scope, db, sender, now);
        await processor.ProcessBatchAsync(default); db.ChangeTracker.Clear(); DeviceToken invalidated = await db.DeviceTokens.SingleAsync(x => x.Id == token.Id); Assert.False(invalidated.IsActive); NotificationAuditRecord invalidAudit = await db.AuditRecords.AsNoTracking().SingleAsync(x => x.Operation == "invalidate_device" && x.EntityId == token.Id.Value); Assert.Equal(token.UserId, invalidAudit.UserId); Assert.Equal("device_token", invalidAudit.EntityType); Assert.Equal(now, invalidAudit.OccurredAtUtc); Assert.Equal("provider=fcm;error=provider.unregistered", invalidAudit.Detail); Assert.Contains(firstDelivery.Id, sender.Deliveries);
        Notification next = Push(token.UserId, "after-invalid", now); NotificationDelivery nextDelivery = next.QueueDelivery(token.Id, "fcm", 3, now); db.Notifications.Add(next); await db.SaveChangesAsync(); await processor.ProcessBatchAsync(default); db.ChangeTracker.Clear(); Assert.DoesNotContain(nextDelivery.Id, sender.Deliveries); Assert.Equal(NotificationStatus.Failed, (await db.Deliveries.AsNoTracking().SingleAsync(x => x.Id == nextDelivery.Id)).Status);
    }

    [Fact]
    public async Task AuditRecordsRejectModificationAndDeletionOnPostgreSql()
    {
        DateTime now = DateTime.UtcNow; await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); NotificationsDbContext db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(); NotificationAuditRecord record = NotificationAuditRecord.Create(Guid.NewGuid(), "append_only", "test", Guid.NewGuid(), "safe", now); db.AuditRecords.Add(record); await db.SaveChangesAsync();
        db.Entry(record).Property(x => x.Detail).CurrentValue = "changed"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear(); NotificationAuditRecord persisted = await db.AuditRecords.SingleAsync(x => x.Id == record.Id); db.AuditRecords.Remove(persisted); await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static NotificationDeliveryProcessor Processor(AsyncServiceScope scope, NotificationsDbContext db, RecordingSender sender, DateTime now) => new(db, [sender], scope.ServiceProvider.GetRequiredService<ITokenProtector>(), new FixedClock(now), Options.Create(new NotificationProcessingOptions { BatchSize = 500, DeliveryLimitPerMinute = 10_000, MaximumBackoffSeconds = 900, ProcessingLeaseSeconds = 300 }), NullLogger<NotificationDeliveryProcessor>.Instance);
    private static async Task<(NotificationsDbContext Db, DeviceToken Token, NotificationDelivery Delivery)> SeedPush(AsyncServiceScope scope, DateTime now)
    {
        NotificationsDbContext db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(); ITokenProtector protector = scope.ServiceProvider.GetRequiredService<ITokenProtector>(); Guid userId = Guid.NewGuid(); string raw = "processor-token-" + Guid.NewGuid().ToString("N"); DeviceToken token = DeviceToken.Register(DeviceTokenId.New(), userId, protector.Protect(raw), protector.Hash(raw), protector.Mask(raw), PushPlatform.Android, PushProvider.Fcm, now); Notification notification = Push(userId, "processor", now); NotificationDelivery delivery = notification.QueueDelivery(token.Id, "fcm", 3, now); db.AddRange(notification, token); await db.SaveChangesAsync(); return (db, token, delivery);
    }
    private static Notification InApp(Guid userId, string category, DateTime now) => Notification.Create(NotificationId.New(), userId, Guid.NewGuid(), category, "order.created.customer", NotificationChannel.InApp, "en", null, "Body", now);
    private static Notification Push(Guid userId, string category, DateTime now) => Notification.Create(NotificationId.New(), userId, Guid.NewGuid(), category, "order.created.customer", NotificationChannel.Push, "en", "Subject", "Body", now);
    private static NotificationAuditRecord AssertAudit(IEnumerable<NotificationAuditRecord> records, string operation, string entityType, Guid entityId, DateTime occurredAtUtc) { NotificationAuditRecord record = Assert.Single(records, x => x.Operation == operation && x.EntityType == entityType && x.EntityId == entityId); Assert.Equal(occurredAtUtc, record.OccurredAtUtc); return record; }
    private sealed class FixedClock(DateTime utcNow) : IClock { public DateTime UtcNow { get; } = utcNow; }
    private sealed class RecordingSender(ProviderSendResult result) : INotificationChannelSender { public string Provider => "fcm"; public NotificationChannel Channel => NotificationChannel.Push; public List<NotificationDeliveryId> Deliveries { get; } = []; public Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken) { Deliveries.Add(request.DeliveryId); return Task.FromResult(result); } }
}
