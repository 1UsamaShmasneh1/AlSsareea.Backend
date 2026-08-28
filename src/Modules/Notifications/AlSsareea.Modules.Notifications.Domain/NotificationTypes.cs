using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Notifications.Domain;

public readonly record struct NotificationId
{
    public NotificationId(Guid value) { if (value == Guid.Empty) throw new DomainException("Notification identifier is required."); Value = value; }
    public Guid Value { get; }
    public static NotificationId New() => new(Guid.NewGuid());
}
public readonly record struct NotificationTemplateId
{
    public NotificationTemplateId(Guid value) { if (value == Guid.Empty) throw new DomainException("Template identifier is required."); Value = value; }
    public Guid Value { get; }
    public static NotificationTemplateId New() => new(Guid.NewGuid());
}
public readonly record struct NotificationDeliveryId
{
    public NotificationDeliveryId(Guid value) { if (value == Guid.Empty) throw new DomainException("Delivery identifier is required."); Value = value; }
    public Guid Value { get; }
    public static NotificationDeliveryId New() => new(Guid.NewGuid());
}
public readonly record struct DeviceTokenId
{
    public DeviceTokenId(Guid value) { if (value == Guid.Empty) throw new DomainException("Device token identifier is required."); Value = value; }
    public Guid Value { get; }
    public static DeviceTokenId New() => new(Guid.NewGuid());
}

public enum NotificationChannel : short { Push = 1, Sms = 2, Email = 3, InApp = 4, WhatsApp = 5 }
public enum NotificationStatus : short { Queued = 1, Processing = 2, ProviderAccepted = 3, Delivered = 4, RetryScheduled = 5, Failed = 6, Suppressed = 7 }
public enum PushPlatform : short { Android = 1, Ios = 2, Web = 3 }
public enum PushProvider : short { Fcm = 1, Apns = 2 }
public enum ProviderFailureKind : short { None = 0, Transient = 1, Permanent = 2, InvalidToken = 3, NotConfigured = 4, RateLimited = 5 }

public static class NotificationRules
{
    public const int TemplateKeyMaximumLength = 120;
    public const int CategoryMaximumLength = 80;
    public const int SubjectMaximumLength = 300;
    public const int BodyMaximumLength = 4000;
    public const int TokenMaximumLength = 4096;
    public const int MaximumAttemptsLimit = 20;
    public static void RequireUtc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new DomainException("Timestamp must be UTC."); }
    public static string Required(string? value, string name, int maximum)
    {
        string result = value?.Trim() ?? string.Empty;
        if (result.Length == 0 || result.Length > maximum) throw new DomainException($"{name} is invalid.");
        return result;
    }
}
