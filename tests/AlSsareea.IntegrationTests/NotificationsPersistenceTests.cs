using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Domain;
using AlSsareea.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class NotificationsPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SchemaMigrationConstraintsIndexesAndIsolationAreCorrect()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); NotificationsDbContext db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(); await db.Database.OpenConnectionAsync();
        Assert.Equal(9L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.tables WHERE table_schema='notifications' AND table_name IN ('notifications','notification_templates','notification_deliveries','notification_attempts','notification_device_tokens','notification_preferences','notification_inbox_messages','notification_audit','notification_outbox_messages')"));
        Assert.Equal(1L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.tables WHERE table_schema='notifications' AND table_name='__ef_migrations_history'"));
        Assert.True(await Scalar<long>(db, "SELECT count(*) FROM information_schema.check_constraints WHERE constraint_schema='notifications'") >= 8);
        Assert.True(await Scalar<long>(db, "SELECT count(*) FROM pg_indexes WHERE schemaname='notifications'") >= 12);
        Assert.Equal(0L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.referential_constraints r JOIN information_schema.table_constraints c ON c.constraint_name=r.constraint_name AND c.constraint_schema=r.constraint_schema WHERE c.constraint_schema='notifications' AND r.unique_constraint_schema <> 'notifications'"));
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task EventReplayCreatesNoDuplicateNotificationsOrDeliveries()
    {
        Guid eventId = Guid.NewGuid(), userId = Guid.NewGuid(); DateTime now = DateTime.UtcNow;
        await using AsyncServiceScope firstScope = fixture.ApiFactory.Services.CreateAsyncScope(); INotificationService first = firstScope.ServiceProvider.GetRequiredService<INotificationService>(); SourceNotification source = new(eventId, userId, "he", "order_updates", "order.created.customer", new Dictionary<string, string> { ["orderNumber"] = "A-17" }, [NotificationChannel.InApp, NotificationChannel.Push], now);
        Assert.True(await first.ConsumeAsync("OrderCreatedIntegrationEvent", source));
        await using AsyncServiceScope replayScope = fixture.ApiFactory.Services.CreateAsyncScope(); Assert.False(await replayScope.ServiceProvider.GetRequiredService<INotificationService>().ConsumeAsync("OrderCreatedIntegrationEvent", source));
        NotificationsDbContext db = replayScope.ServiceProvider.GetRequiredService<NotificationsDbContext>(); Assert.Equal(2, await db.Notifications.CountAsync(x => x.SourceEventId == eventId)); Assert.Equal(2, await db.Deliveries.CountAsync(x => db.Notifications.Where(n => n.SourceEventId == eventId).Select(n => n.Id).Contains(x.NotificationId))); Assert.Equal(1, await db.InboxMessages.CountAsync(x => x.Id == eventId));
    }
    [Fact]
    public async Task DevicePreferenceAttemptAndHistoryPersist()
    {
        DateTime now = DateTime.UtcNow; Guid user = Guid.NewGuid(); Notification notification = Notification.Create(NotificationId.New(), user, Guid.NewGuid(), "order_updates", "order.created.customer", NotificationChannel.InApp, "ar", null, "تم الاستلام", now); NotificationDelivery delivery = notification.QueueDelivery(null, "inapp", 1, now); delivery.Claim(now); delivery.RecordDelivered("local", now); notification.SynchronizeStatus(delivery.Status, now);
        DeviceToken token = DeviceToken.Register(DeviceTokenId.New(), user, "protected-token-value", new string('b', 64), "abcd…wxyz", PushPlatform.Android, PushProvider.Fcm, now); NotificationPreference preference = NotificationPreference.Create(user, "order_updates", NotificationChannel.Sms, false, now);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); NotificationsDbContext db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(); db.Notifications.Add(notification); db.DeviceTokens.Add(token); db.Preferences.Add(preference); db.AuditRecords.Add(NotificationAuditRecord.Create(user, "test", "notification", notification.Id.Value, null, now)); db.OutboxMessages.Add(NotificationOutboxMessage.Create(Guid.NewGuid(), "NotificationDeliveredIntegrationEvent", "{}", now, now)); await db.SaveChangesAsync();
        Assert.Single(await db.Attempts.Where(x => x.NotificationDeliveryId == delivery.Id).ToArrayAsync()); Assert.True(await db.DeviceTokens.AnyAsync(x => x.TokenHash == token.TokenHash && x.IsActive)); Assert.False((await db.Preferences.SingleAsync(x => x.Id == preference.Id)).Enabled); Assert.True(await db.AuditRecords.AnyAsync(x => x.EntityId == notification.Id.Value)); Assert.True(await db.OutboxMessages.AnyAsync());
    }
    [Fact]
    public async Task ConcurrentWorkersCannotClaimTheSameDelivery()
    {
        DateTime claimAt = DateTime.UtcNow.AddHours(1); Notification notification = Notification.Create(NotificationId.New(), Guid.NewGuid(), Guid.NewGuid(), "order_updates", "order.created.customer", NotificationChannel.InApp, "en", null, "Received", claimAt); NotificationDelivery delivery = notification.QueueDelivery(null, "inapp", 1, claimAt);
        await using (AsyncServiceScope seedScope = fixture.ApiFactory.Services.CreateAsyncScope()) { NotificationsDbContext seed = seedScope.ServiceProvider.GetRequiredService<NotificationsDbContext>(); seed.Notifications.Add(notification); await seed.SaveChangesAsync(); }
        await using AsyncServiceScope firstScope = fixture.ApiFactory.Services.CreateAsyncScope(); await using AsyncServiceScope secondScope = fixture.ApiFactory.Services.CreateAsyncScope(); NotificationsDbContext first = firstScope.ServiceProvider.GetRequiredService<NotificationsDbContext>(); NotificationsDbContext second = secondScope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        Task<int> firstClaim = Claim(first, delivery.Id, claimAt); Task<int> secondClaim = Claim(second, delivery.Id, claimAt); int[] claims = await Task.WhenAll(firstClaim, secondClaim);
        Assert.Equal([0, 1], claims.Order());
    }
    private static Task<int> Claim(NotificationsDbContext db, NotificationDeliveryId id, DateTime now) => db.Deliveries.Where(x => x.Id == id && (x.Status == NotificationStatus.Queued || x.Status == NotificationStatus.RetryScheduled) && x.NextAttemptAtUtc <= now).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NotificationStatus.Processing).SetProperty(x => x.NextAttemptAtUtc, (DateTime?)null).SetProperty(x => x.UpdatedAtUtc, now).SetProperty(x => x.ConcurrencyStamp, Guid.NewGuid()));
    private static async Task<T> Scalar<T>(NotificationsDbContext db, string sql) { if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open) await db.Database.OpenConnectionAsync(); await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; object? value = await command.ExecuteScalarAsync(); return (T)Convert.ChangeType(value!, typeof(T), System.Globalization.CultureInfo.InvariantCulture); }
}
