using AlSsareea.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Notifications.Infrastructure.Persistence;

internal static class NotificationPropertyExtensions
{
    public static PropertyBuilder<TId> StrongId<TId>(this PropertyBuilder<TId> property, Func<TId, Guid> to, Func<Guid, TId> from) where TId : struct => property.HasConversion(x => to(x), x => from(x)).HasColumnType("uuid");
}
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications", NotificationsPersistence.Schema, t => { t.HasCheckConstraint("ck_notifications_channel", "channel BETWEEN 1 AND 5"); t.HasCheckConstraint("ck_notifications_status", "status BETWEEN 1 AND 7"); t.HasCheckConstraint("ck_notifications_body", "char_length(body) > 0"); }); b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new(x)); b.Property(x => x.Category).HasMaxLength(NotificationRules.CategoryMaximumLength); b.Property(x => x.TemplateKey).HasMaxLength(NotificationRules.TemplateKeyMaximumLength); b.Property(x => x.Channel).HasConversion<short>(); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.Language).HasMaxLength(10); b.Property(x => x.Subject).HasMaxLength(NotificationRules.SubjectMaximumLength); b.Property(x => x.Body).HasMaxLength(NotificationRules.BodyMaximumLength); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.Ignore(x => x.DomainEvents); b.HasIndex(x => new { x.SourceEventId, x.UserId, x.Channel }).IsUnique(); b.HasIndex(x => new { x.UserId, x.CreatedAtUtc }); b.HasIndex(x => new { x.UserId, x.ReadAtUtc }).HasFilter("channel = 4"); b.HasMany(x => x.Deliveries).WithOne().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.NoAction); b.Navigation(x => x.Deliveries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> b) { b.ToTable("notification_deliveries", NotificationsPersistence.Schema, t => { t.HasCheckConstraint("ck_notification_deliveries_attempts", "attempt_count >= 0 AND attempt_count <= maximum_attempts"); t.HasCheckConstraint("ck_notification_deliveries_status", "status BETWEEN 1 AND 7"); }); b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new(x)); b.Property(x => x.NotificationId).StrongId(x => x.Value, x => new(x)); b.Property(x => x.DeviceTokenId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new DeviceTokenId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.Provider).HasMaxLength(80); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.LastErrorCode).HasMaxLength(160); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc }); b.HasIndex(x => x.NotificationId); b.HasMany(x => x.Attempts).WithOne().HasForeignKey(x => x.NotificationDeliveryId).OnDelete(DeleteBehavior.NoAction); b.Navigation(x => x.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field); }
}
internal sealed class AttemptConfiguration : IEntityTypeConfiguration<NotificationAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationAttempt> b) { b.ToTable("notification_attempts", NotificationsPersistence.Schema); b.HasKey(x => x.Id); b.Property(x => x.NotificationDeliveryId).StrongId(x => x.Value, x => new(x)); b.Property(x => x.FailureKind).HasConversion<short>(); b.Property(x => x.ErrorCode).HasMaxLength(160); b.Property(x => x.ProviderMessageId).HasMaxLength(300); b.HasIndex(x => new { x.NotificationDeliveryId, x.AttemptNumber }).IsUnique(); }
}
internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> b) { b.ToTable("notification_device_tokens", NotificationsPersistence.Schema, t => { t.HasCheckConstraint("ck_notification_device_tokens_platform", "platform BETWEEN 1 AND 3"); t.HasCheckConstraint("ck_notification_device_tokens_provider", "provider BETWEEN 1 AND 2"); }); b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new(x)); b.Property(x => x.TokenCiphertext).HasMaxLength(NotificationRules.TokenMaximumLength); b.Property(x => x.TokenHash).HasMaxLength(64); b.Property(x => x.TokenMask).HasMaxLength(32); b.Property(x => x.Platform).HasConversion<short>(); b.Property(x => x.Provider).HasConversion<short>(); b.Property(x => x.DeactivationReason).HasMaxLength(80); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.Ignore(x => x.DomainEvents); b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => new { x.UserId, x.IsActive }); }
}
internal sealed class PreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> b) { b.ToTable("notification_preferences", NotificationsPersistence.Schema); b.HasKey(x => x.Id); b.Property(x => x.Category).HasMaxLength(NotificationRules.CategoryMaximumLength); b.Property(x => x.Channel).HasConversion<short>(); b.HasIndex(x => new { x.UserId, x.Category, x.Channel }).IsUnique(); }
}
internal sealed class TemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    private static readonly DateTime SeedTime = new(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);
    public void Configure(EntityTypeBuilder<NotificationTemplate> b)
    {
        b.ToTable("notification_templates", NotificationsPersistence.Schema); b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new(x)); b.Property(x => x.Key).HasMaxLength(NotificationRules.TemplateKeyMaximumLength); b.Property(x => x.Channel).HasConversion<short>(); b.Property(x => x.Language).HasMaxLength(10); b.Property(x => x.Subject).HasMaxLength(NotificationRules.SubjectMaximumLength); b.Property(x => x.Body).HasMaxLength(NotificationRules.BodyMaximumLength); b.Ignore(x => x.DomainEvents); b.HasIndex(x => new { x.Key, x.Channel, x.Language }).IsUnique();
        List<object> seeds = [];
        AddSeeds(seeds, "order.created.customer", "تم استلام طلبك {{orderNumber}}", "ההזמנה שלך {{orderNumber}} התקבלה", "Your order {{orderNumber}} was received");
        AddSeeds(seeds, "order.created.merchant", "طلب جديد {{orderNumber}}", "הזמנה חדשה {{orderNumber}}", "New order {{orderNumber}}");
        AddSeeds(seeds, "delivery.status.customer", "تحديث التوصيل للطلب {{orderId}}", "עדכון משלוח להזמנה {{orderId}}", "Delivery update for order {{orderId}}");
        AddSeeds(seeds, "dispatch.offer.driver", "لديك عرض توصيل جديد", "יש לך הצעת משלוח חדשה", "You have a new delivery offer");
        b.HasData(seeds);
    }
    private static void AddSeeds(List<object> seeds, string key, string ar, string he, string en)
    {
        AddLanguage(seeds, key, "ar", ar); AddLanguage(seeds, key, "he", he); AddLanguage(seeds, key, "en", en);
    }
    private static void AddLanguage(List<object> seeds, string key, string language, string body)
    {
        foreach (NotificationChannel channel in new[] { NotificationChannel.InApp, NotificationChannel.Push }) { Guid id = StableId($"{key}:{channel}:{language}"); seeds.Add(new { Id = new NotificationTemplateId(id), Key = key, Channel = channel, Language = language, Subject = (string?)null, Body = body, IsActive = true, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime }); }
    }
    private static Guid StableId(string value) => new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}
internal sealed class InboxConfiguration : IEntityTypeConfiguration<NotificationInboxMessage>
{
    public void Configure(EntityTypeBuilder<NotificationInboxMessage> b) { b.ToTable("notification_inbox_messages", NotificationsPersistence.Schema); b.HasKey(x => x.Id); b.Property(x => x.EventType).HasMaxLength(200); b.HasIndex(x => x.ProcessedAtUtc); }
}
internal sealed class AuditConfiguration : IEntityTypeConfiguration<NotificationAuditRecord>
{
    public void Configure(EntityTypeBuilder<NotificationAuditRecord> b) { b.ToTable("notification_audit", NotificationsPersistence.Schema); b.HasKey(x => x.Id); b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.EntityType).HasMaxLength(80); b.Property(x => x.Detail).HasMaxLength(300); b.HasIndex(x => new { x.UserId, x.OccurredAtUtc }); }
}
internal sealed class OutboxConfiguration : IEntityTypeConfiguration<NotificationOutboxMessage>
{
    public void Configure(EntityTypeBuilder<NotificationOutboxMessage> b) { b.ToTable("notification_outbox_messages", NotificationsPersistence.Schema, t => { t.HasCheckConstraint("ck_notification_outbox_payload", "jsonb_typeof(payload) = 'object'"); t.HasCheckConstraint("ck_notification_outbox_attempts", "attempt_count >= 0"); }); b.HasKey(x => x.Id); b.Property(x => x.EventType).HasMaxLength(200); b.Property(x => x.Payload).HasColumnType("jsonb"); b.Property(x => x.ErrorCode).HasMaxLength(160); b.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc }).HasFilter("processed_at_utc IS NULL"); }
}
