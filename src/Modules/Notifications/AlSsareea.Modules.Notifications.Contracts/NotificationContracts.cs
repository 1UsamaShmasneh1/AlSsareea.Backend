namespace AlSsareea.Modules.Notifications.Contracts;

public sealed record NotificationListItem(Guid Id, string Category, string TemplateKey, short Channel, string Language, string? Subject, string Body, short Status, DateTime CreatedAtUtc, DateTime? ReadAtUtc);
public sealed record NotificationListResponse(IReadOnlyList<NotificationListItem> Items, int Page, int PageSize, int TotalCount, int UnreadCount);
public sealed record RegisterDeviceTokenRequest(string Token, short Platform, short Provider);
public sealed record DeviceTokenResponse(Guid Id, short Platform, short Provider, string TokenMask, bool Active, DateTime UpdatedAtUtc);
public sealed record NotificationPreferenceItem(string Category, short Channel, bool Enabled);
public sealed record NotificationPreferencesResponse(IReadOnlyList<NotificationPreferenceItem> Items);
public sealed record UpdateNotificationPreferencesRequest(IReadOnlyList<NotificationPreferenceItem> Items);
