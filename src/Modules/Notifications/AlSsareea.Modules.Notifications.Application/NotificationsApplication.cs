using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Application.Localization;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Notifications.Contracts;
using AlSsareea.Modules.Notifications.Domain;

namespace AlSsareea.Modules.Notifications.Application;

public enum NotificationOperationStatus { Ok, Created, BadRequest, NotFound, Forbidden, Conflict }
public sealed record NotificationOperationResult<T>(NotificationOperationStatus Status, T? Value = default, string? ErrorCode = null);
public sealed record RenderedTemplate(string Language, string? Subject, string Body);
public sealed record NotificationRecipient(Guid UserId, string Language);
public sealed record SourceNotification(Guid EventId, Guid UserId, string Language, string Category, string TemplateKey, IReadOnlyDictionary<string, string> Parameters, IReadOnlyCollection<NotificationChannel> Channels, DateTime OccurredAtUtc);
public sealed record ProviderSendRequest(NotificationDeliveryId DeliveryId, NotificationChannel Channel, string Provider, string? Token, string? Subject, string Body);
public sealed record ProviderSendResult(bool Accepted, bool Delivered, ProviderFailureKind FailureKind, string? ErrorCode = null, string? ProviderMessageId = null);

public interface INotificationStore
{
    Task<NotificationListResponse> ListAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Notification?> FindOwnedAsync(NotificationId id, Guid userId, CancellationToken cancellationToken);
    Task<int> MarkAllReadAsync(Guid userId, DateTime now, CancellationToken cancellationToken);
    Task<DeviceToken?> FindTokenAsync(DeviceTokenId id, Guid userId, CancellationToken cancellationToken);
    Task<DeviceToken?> FindTokenByHashAsync(string hash, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceToken>> ActiveTokensAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationPreference>> PreferencesAsync(Guid userId, CancellationToken cancellationToken);
    Task<NotificationTemplate?> FindTemplateAsync(string key, NotificationChannel channel, string language, CancellationToken cancellationToken);
    Task<bool> SourceEventProcessedAsync(Guid eventId, CancellationToken cancellationToken);
    void Add(Notification notification);
    void Add(DeviceToken token);
    void Add(NotificationPreference preference);
    void AddInbox(Guid eventId, string eventType, DateTime occurredAtUtc, DateTime processedAtUtc);
    void AddAudit(Guid userId, string operation, string entityType, Guid entityId, string? detail, DateTime now);
    Task SaveAsync(CancellationToken cancellationToken);
}
public interface ITemplateRenderer { RenderedTemplate Render(NotificationTemplate notificationTemplate, IReadOnlyDictionary<string, string> parameters); }
public interface ITokenProtector { string Protect(string token); string Unprotect(string protectedToken); string Hash(string token); string Mask(string token); }
public interface INotificationChannelSender { string Provider { get; } NotificationChannel Channel { get; } Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken); }

public interface INotificationService
{
    Task<NotificationListResponse> ListAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<NotificationOperationResult<bool>> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NotificationOperationResult<DeviceTokenResponse>> RegisterDeviceAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default);
    Task<NotificationOperationResult<bool>> UnregisterDeviceAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default);
    Task<NotificationPreferencesResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NotificationOperationResult<NotificationPreferencesResponse>> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default);
    Task<bool> ConsumeAsync(string eventType, SourceNotification source, CancellationToken cancellationToken = default);
}

public sealed class SafeTemplateRenderer : ITemplateRenderer
{
    public RenderedTemplate Render(NotificationTemplate notificationTemplate, IReadOnlyDictionary<string, string> parameters)
    {
        string subject = RenderValue(notificationTemplate.Subject, parameters) ?? string.Empty; string body = RenderValue(notificationTemplate.Body, parameters) ?? string.Empty;
        return new(notificationTemplate.Language, subject.Length == 0 ? null : subject, body);
    }
    private static string? RenderValue(string? template, IReadOnlyDictionary<string, string> parameters)
    {
        if (template is null) return null;
        System.Text.StringBuilder result = new();
        for (int index = 0; index < template.Length;)
        {
            int start = template.IndexOf("{{", index, StringComparison.Ordinal); if (start < 0) { result.Append(template, index, template.Length - index); break; }
            result.Append(template, index, start - index); int end = template.IndexOf("}}", start + 2, StringComparison.Ordinal); if (end < 0) throw new DomainException("Template contains an unclosed parameter.");
            string key = template[(start + 2)..end].Trim(); if (key.Length == 0 || !parameters.TryGetValue(key, out string? value)) throw new DomainException($"Template parameter '{key}' is missing.");
            result.Append(value); index = end + 2;
        }
        return result.ToString();
    }
}

public sealed class NotificationService(INotificationStore store, ITemplateRenderer renderer, ITokenProtector tokens, IClock clock) : INotificationService
{
    private static readonly HashSet<string> Languages = [SupportedCultures.Arabic, SupportedCultures.Hebrew, SupportedCultures.English];
    public Task<NotificationListResponse> ListAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default) => store.ListAsync(userId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);
    public async Task<NotificationOperationResult<bool>> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || notificationId == Guid.Empty) return new(NotificationOperationStatus.BadRequest, ErrorCode: "notifications.invalid_request");
        Notification? notification = await store.FindOwnedAsync(new(notificationId), userId, cancellationToken); if (notification is null) return new(NotificationOperationStatus.NotFound, ErrorCode: "notifications.not_found");
        bool changed = notification.MarkRead(userId, clock.UtcNow); if (changed) { store.AddAudit(userId, "mark_read", "notification", notificationId, null, clock.UtcNow); await store.SaveAsync(cancellationToken); }
        return new(NotificationOperationStatus.Ok, true);
    }
    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default) => store.MarkAllReadAsync(userId, clock.UtcNow, cancellationToken);
    public async Task<NotificationOperationResult<DeviceTokenResponse>> RegisterDeviceAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
    {
        string raw = request.Token?.Trim() ?? string.Empty; if (userId == Guid.Empty || raw.Length is < 16 or > NotificationRules.TokenMaximumLength || !Enum.IsDefined((PushPlatform)request.Platform) || !Enum.IsDefined((PushProvider)request.Provider)) return new(NotificationOperationStatus.BadRequest, ErrorCode: "notifications.invalid_device_token");
        string hash = tokens.Hash(raw); DeviceToken? existing = await store.FindTokenByHashAsync(hash, cancellationToken);
        if (existing is not null && existing.UserId != userId) return new(NotificationOperationStatus.Conflict, ErrorCode: "notifications.device_token_conflict");
        DateTime now = clock.UtcNow; if (existing is null) { existing = DeviceToken.Register(DeviceTokenId.New(), userId, tokens.Protect(raw), hash, tokens.Mask(raw), (PushPlatform)request.Platform, (PushProvider)request.Provider, now); store.Add(existing); }
        else if (existing.IsActive) existing.Replace(tokens.Protect(raw), hash, tokens.Mask(raw), now);
        else return new(NotificationOperationStatus.Conflict, ErrorCode: "notifications.device_token_inactive");
        store.AddAudit(userId, "register_device", "device_token", existing.Id.Value, existing.TokenMask, now); await store.SaveAsync(cancellationToken);
        return new(existing.CreatedAtUtc == now ? NotificationOperationStatus.Created : NotificationOperationStatus.Ok, new(existing.Id.Value, (short)existing.Platform, (short)existing.Provider, existing.TokenMask, existing.IsActive, existing.UpdatedAtUtc));
    }
    public async Task<NotificationOperationResult<bool>> UnregisterDeviceAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || tokenId == Guid.Empty) return new(NotificationOperationStatus.BadRequest, ErrorCode: "notifications.invalid_request"); DeviceToken? token = await store.FindTokenAsync(new(tokenId), userId, cancellationToken); if (token is null) return new(NotificationOperationStatus.NotFound, ErrorCode: "notifications.device_not_found");
        if (token.Deactivate("user_unregistered", clock.UtcNow)) { store.AddAudit(userId, "unregister_device", "device_token", tokenId, token.TokenMask, clock.UtcNow); await store.SaveAsync(cancellationToken); }
        return new(NotificationOperationStatus.Ok, true);
    }
    public async Task<NotificationPreferencesResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default) => new((await store.PreferencesAsync(userId, cancellationToken)).Select(x => new NotificationPreferenceItem(x.Category, (short)x.Channel, x.Enabled)).ToArray());
    public async Task<NotificationOperationResult<NotificationPreferencesResponse>> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || request.Items is null || request.Items.Count > 100 || request.Items.Any(x => string.IsNullOrWhiteSpace(x.Category) || x.Category.Length > NotificationRules.CategoryMaximumLength || !Enum.IsDefined((NotificationChannel)x.Channel)) || request.Items.GroupBy(x => (x.Category.Trim(), x.Channel)).Any(x => x.Count() > 1)) return new(NotificationOperationStatus.BadRequest, ErrorCode: "notifications.invalid_preferences");
        List<NotificationPreference> current = [.. await store.PreferencesAsync(userId, cancellationToken)]; DateTime now = clock.UtcNow;
        foreach (NotificationPreferenceItem item in request.Items) { NotificationPreference? preference = current.SingleOrDefault(x => x.Category == item.Category.Trim() && (short)x.Channel == item.Channel); if (preference is null) store.Add(NotificationPreference.Create(userId, item.Category, (NotificationChannel)item.Channel, item.Enabled, now)); else preference.Set(item.Enabled, now); }
        store.AddAudit(userId, "update_preferences", "recipient_preferences", userId, $"count={request.Items.Count}", now); await store.SaveAsync(cancellationToken); return new(NotificationOperationStatus.Ok, await GetPreferencesAsync(userId, cancellationToken));
    }
    public async Task<bool> ConsumeAsync(string eventType, SourceNotification source, CancellationToken cancellationToken = default)
    {
        if (await store.SourceEventProcessedAsync(source.EventId, cancellationToken)) return false;
        IReadOnlyList<NotificationPreference> preferences = await store.PreferencesAsync(source.UserId, cancellationToken); DateTime now = clock.UtcNow;
        foreach (NotificationChannel channel in source.Channels.Distinct())
        {
            bool enabled = preferences.SingleOrDefault(x => x.Category == source.Category && x.Channel == channel)?.Enabled ?? true; if (!enabled) continue;
            string requestedLanguage = Languages.Contains(source.Language) ? source.Language : SupportedCultures.Default;
            NotificationTemplate? template = await store.FindTemplateAsync(source.TemplateKey, channel, requestedLanguage, cancellationToken) ?? (requestedLanguage == SupportedCultures.Default ? null : await store.FindTemplateAsync(source.TemplateKey, channel, SupportedCultures.Default, cancellationToken));
            if (template is null) throw new DomainException($"Active template '{source.TemplateKey}' for channel '{channel}' is missing.");
            RenderedTemplate rendered = renderer.Render(template, source.Parameters); Notification notification = Notification.Create(NotificationId.New(), source.UserId, source.EventId, source.Category, source.TemplateKey, channel, rendered.Language, rendered.Subject, rendered.Body, now);
            if (channel == NotificationChannel.Push) { IReadOnlyList<DeviceToken> active = await store.ActiveTokensAsync(source.UserId, cancellationToken); foreach (DeviceToken token in active) notification.QueueDelivery(token.Id, token.Provider == PushProvider.Fcm ? "fcm" : "apns", 5, now); if (active.Count == 0) notification.QueueDelivery(null, "push-unavailable", 1, now); }
            else notification.QueueDelivery(null, channel.ToString().ToLowerInvariant(), channel == NotificationChannel.InApp ? 1 : 5, now);
            store.Add(notification);
        }
        store.AddInbox(source.EventId, eventType, source.OccurredAtUtc, now); store.AddAudit(source.UserId, "consume_event", "inbox_message", source.EventId, eventType, now); await store.SaveAsync(cancellationToken); return true;
    }
}
