using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Domain;

namespace AlSsareea.UnitTests.Notifications;

public sealed class NotificationDomainTests
{
    private static readonly DateTime Now = new(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
    [Fact] public void ValidNotificationCreatesQueuedDelivery() { Notification value = Create(); NotificationDelivery delivery = value.QueueDelivery(null, "inapp", 1, Now); Assert.Equal(NotificationStatus.Queued, value.Status); Assert.Equal(NotificationStatus.Queued, delivery.Status); }
    [Fact] public void MissingRecipientIsRejected() => Assert.Throws<DomainException>(() => Notification.Create(NotificationId.New(), Guid.Empty, Guid.NewGuid(), "orders", "key", NotificationChannel.InApp, "ar", null, "body", Now));
    [Fact] public void NonUtcTimestampIsRejected() => Assert.Throws<DomainException>(() => Notification.Create(NotificationId.New(), Guid.NewGuid(), Guid.NewGuid(), "orders", "key", NotificationChannel.InApp, "ar", null, "body", DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    [Fact] public void ReadTransitionIsIdempotent() { Guid user = Guid.NewGuid(); Notification value = Notification.Create(NotificationId.New(), user, Guid.NewGuid(), "orders", "key", NotificationChannel.InApp, "ar", null, "body", Now); Assert.True(value.MarkRead(user, Now)); Assert.False(value.MarkRead(user, Now.AddSeconds(1))); }
    [Fact] public void AnotherUserCannotReadNotification() { Notification value = Create(); Assert.Throws<DomainException>(() => value.MarkRead(Guid.NewGuid(), Now)); }
    [Fact] public void TransientFailureSchedulesBoundedRetry() { NotificationDelivery delivery = Create().QueueDelivery(null, "email", 3, Now); delivery.Claim(Now); delivery.RecordFailure(ProviderFailureKind.Transient, "timeout", Now, TimeSpan.FromSeconds(5)); Assert.Equal(NotificationStatus.RetryScheduled, delivery.Status); Assert.Equal(Now.AddSeconds(5), delivery.NextAttemptAtUtc); }
    [Fact] public void PermanentFailureIsNotRetried() { NotificationDelivery delivery = Create().QueueDelivery(null, "email", 3, Now); delivery.Claim(Now); delivery.RecordFailure(ProviderFailureKind.Permanent, "rejected", Now, TimeSpan.FromSeconds(5)); Assert.Equal(NotificationStatus.Failed, delivery.Status); Assert.Null(delivery.NextAttemptAtUtc); }
    [Fact] public void MaximumAttemptsStopsRetries() { NotificationDelivery delivery = Create().QueueDelivery(null, "email", 1, Now); delivery.Claim(Now); delivery.RecordFailure(ProviderFailureKind.Transient, "timeout", Now, TimeSpan.FromSeconds(5)); Assert.Equal(NotificationStatus.Failed, delivery.Status); }
    [Fact] public void InvalidTokenDeactivationIsIdempotent() { DeviceToken token = DeviceToken.Register(DeviceTokenId.New(), Guid.NewGuid(), "ciphertext-long-enough", new string('a', 64), "abcd…wxyz", PushPlatform.Android, PushProvider.Fcm, Now); Assert.True(token.Deactivate("provider_invalid", Now)); Assert.False(token.Deactivate("provider_invalid", Now.AddSeconds(1))); Assert.False(token.IsActive); }
    [Theory]
    [InlineData("ar", "تم استلام طلبك 123")]
    [InlineData("he", "הזמנה 123")]
    [InlineData("en", "Order 123")]
    public void TemplateRenderingSupportsAllLanguages(string language, string expected) { string body = language switch { "ar" => "تم استلام طلبك {{number}}", "he" => "הזמנה {{number}}", _ => "Order {{number}}" }; NotificationTemplate template = NotificationTemplate.Create(NotificationTemplateId.New(), "order.created", NotificationChannel.InApp, language, null, body, Now); RenderedTemplate rendered = new SafeTemplateRenderer().Render(template, new Dictionary<string, string> { ["number"] = "123" }); Assert.Equal(expected, rendered.Body); }
    [Fact] public void MissingTemplateParameterFails() { NotificationTemplate template = NotificationTemplate.Create(NotificationTemplateId.New(), "order.created", NotificationChannel.InApp, "ar", null, "{{number}}", Now); Assert.Throws<DomainException>(() => new SafeTemplateRenderer().Render(template, new Dictionary<string, string>())); }
    [Fact] public void PreferenceCanSuppressChannel() { NotificationPreference value = NotificationPreference.Create(Guid.NewGuid(), "order_updates", NotificationChannel.Sms, true, Now); value.Set(false, Now.AddSeconds(1)); Assert.False(value.Enabled); }
    private static Notification Create() => Notification.Create(NotificationId.New(), Guid.NewGuid(), Guid.NewGuid(), "order_updates", "order.created", NotificationChannel.InApp, "ar", null, "body", Now);
}
