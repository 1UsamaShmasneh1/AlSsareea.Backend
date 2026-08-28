using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Notifications.Domain;

public sealed class Notification : AggregateRoot<NotificationId>
{
    private readonly List<NotificationDelivery> _deliveries = [];
    private Notification() : base(default) { }
    private Notification(NotificationId id, Guid userId, Guid sourceEventId, string category, string templateKey, NotificationChannel channel, string language, string? subject, string body, DateTime now) : base(id)
    {
        if (userId == Guid.Empty || sourceEventId == Guid.Empty) throw new DomainException("Notification recipient and source event are required.");
        NotificationRules.RequireUtc(now);
        UserId = userId; SourceEventId = sourceEventId; Category = NotificationRules.Required(category, nameof(category), NotificationRules.CategoryMaximumLength);
        TemplateKey = NotificationRules.Required(templateKey, nameof(templateKey), NotificationRules.TemplateKeyMaximumLength); Channel = channel;
        Language = NotificationRules.Required(language, nameof(language), 10); Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
        Body = NotificationRules.Required(body, nameof(body), NotificationRules.BodyMaximumLength); Status = NotificationStatus.Queued; CreatedAtUtc = UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid();
    }
    public Guid UserId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string TemplateKey { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public string Language { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<NotificationDelivery> Deliveries => _deliveries.AsReadOnly();
    public static Notification Create(NotificationId id, Guid userId, Guid sourceEventId, string category, string templateKey, NotificationChannel channel, string language, string? subject, string body, DateTime now) => new(id, userId, sourceEventId, category, templateKey, channel, language, subject, body, now);
    public NotificationDelivery QueueDelivery(DeviceTokenId? tokenId, string provider, int maximumAttempts, DateTime now)
    {
        if (Channel == NotificationChannel.InApp && _deliveries.Count != 0) throw new DomainException("In-app notification has a single durable delivery.");
        NotificationDelivery delivery = NotificationDelivery.Create(NotificationDeliveryId.New(), Id, tokenId, provider, maximumAttempts, now); _deliveries.Add(delivery); return delivery;
    }
    public bool MarkRead(Guid userId, DateTime now)
    {
        if (UserId != userId) throw new DomainException("Notification does not belong to this user.");
        if (Channel != NotificationChannel.InApp) throw new DomainException("Only in-app notifications can be read.");
        if (ReadAtUtc is not null) return false;
        NotificationRules.RequireUtc(now); ReadAtUtc = now; UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); return true;
    }
    public void SynchronizeStatus(NotificationStatus status, DateTime now) { NotificationRules.RequireUtc(now); Status = status; UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
}

public sealed class NotificationDelivery : Entity<NotificationDeliveryId>
{
    private readonly List<NotificationAttempt> _attempts = [];
    private NotificationDelivery() : base(default) { }
    private NotificationDelivery(NotificationDeliveryId id, NotificationId notificationId, DeviceTokenId? tokenId, string provider, int maximumAttempts, DateTime now) : base(id)
    {
        if (maximumAttempts is < 1 or > NotificationRules.MaximumAttemptsLimit) throw new DomainException("Maximum attempts is invalid.");
        NotificationRules.RequireUtc(now); NotificationId = notificationId; DeviceTokenId = tokenId; Provider = NotificationRules.Required(provider, nameof(provider), 80); MaximumAttempts = maximumAttempts; Status = NotificationStatus.Queued; NextAttemptAtUtc = now; CreatedAtUtc = UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid();
    }
    public NotificationId NotificationId { get; private set; }
    public DeviceTokenId? DeviceTokenId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaximumAttempts { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public string? LastErrorCode { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<NotificationAttempt> Attempts => _attempts.AsReadOnly();
    public static NotificationDelivery Create(NotificationDeliveryId id, NotificationId notificationId, DeviceTokenId? tokenId, string provider, int maximumAttempts, DateTime now) => new(id, notificationId, tokenId, provider, maximumAttempts, now);
    public void Claim(DateTime now) { if (Status is not (NotificationStatus.Queued or NotificationStatus.RetryScheduled) || NextAttemptAtUtc > now) throw new DomainException("Delivery is not ready."); Touch(now); Status = NotificationStatus.Processing; NextAttemptAtUtc = null; }
    public void RecordAccepted(string? providerMessageId, DateTime now) { AddAttempt(true, ProviderFailureKind.None, null, providerMessageId, now); Status = NotificationStatus.ProviderAccepted; Touch(now); }
    public void RecordDelivered(string? providerMessageId, DateTime now) { AddAttempt(true, ProviderFailureKind.None, null, providerMessageId, now); Status = NotificationStatus.Delivered; Touch(now); }
    public void RecordFailure(ProviderFailureKind kind, string errorCode, DateTime now, TimeSpan retryDelay)
    {
        if (kind == ProviderFailureKind.None) throw new DomainException("Failure kind is required.");
        AddAttempt(false, kind, errorCode, null, now); LastErrorCode = NotificationRules.Required(errorCode, nameof(errorCode), 160);
        bool retryable = kind is ProviderFailureKind.Transient or ProviderFailureKind.RateLimited;
        if (retryable && AttemptCount < MaximumAttempts) { Status = NotificationStatus.RetryScheduled; NextAttemptAtUtc = now.Add(retryDelay); }
        else { Status = NotificationStatus.Failed; NextAttemptAtUtc = null; }
        Touch(now);
    }
    private void AddAttempt(bool succeeded, ProviderFailureKind kind, string? error, string? providerMessageId, DateTime now)
    {
        if (AttemptCount >= MaximumAttempts) throw new DomainException("Maximum delivery attempts exceeded.");
        AttemptCount++; _attempts.Add(NotificationAttempt.Create(Guid.NewGuid(), Id, AttemptCount, succeeded, kind, error, providerMessageId, now));
    }
    private void Touch(DateTime now) { NotificationRules.RequireUtc(now); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
}

public sealed class NotificationAttempt : Entity<Guid>
{
    private NotificationAttempt() : base(default) { }
    private NotificationAttempt(Guid id, NotificationDeliveryId deliveryId, int number, bool succeeded, ProviderFailureKind failureKind, string? errorCode, string? providerMessageId, DateTime now) : base(id) { NotificationDeliveryId = deliveryId; AttemptNumber = number; Succeeded = succeeded; FailureKind = failureKind; ErrorCode = errorCode; ProviderMessageId = providerMessageId; AttemptedAtUtc = now; }
    public NotificationDeliveryId NotificationDeliveryId { get; private set; }
    public int AttemptNumber { get; private set; }
    public bool Succeeded { get; private set; }
    public ProviderFailureKind FailureKind { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public DateTime AttemptedAtUtc { get; private set; }
    public static NotificationAttempt Create(Guid id, NotificationDeliveryId deliveryId, int number, bool succeeded, ProviderFailureKind failureKind, string? errorCode, string? providerMessageId, DateTime now) => new(id, deliveryId, number, succeeded, failureKind, errorCode, providerMessageId, now);
}

public sealed class DeviceToken : AggregateRoot<DeviceTokenId>
{
    private DeviceToken() : base(default) { }
    private DeviceToken(DeviceTokenId id, Guid userId, string tokenCiphertext, string tokenHash, string tokenMask, PushPlatform platform, PushProvider provider, DateTime now) : base(id)
    {
        if (userId == Guid.Empty) throw new DomainException("Token owner is required."); NotificationRules.RequireUtc(now);
        UserId = userId; TokenCiphertext = NotificationRules.Required(tokenCiphertext, nameof(tokenCiphertext), NotificationRules.TokenMaximumLength); TokenHash = NotificationRules.Required(tokenHash, nameof(tokenHash), 64); TokenMask = NotificationRules.Required(tokenMask, nameof(tokenMask), 32); Platform = platform; Provider = provider; IsActive = true; CreatedAtUtc = UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid();
    }
    public Guid UserId { get; private set; }
    public string TokenCiphertext { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public string TokenMask { get; private set; } = string.Empty;
    public PushPlatform Platform { get; private set; }
    public PushProvider Provider { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? DeactivatedAtUtc { get; private set; }
    public string? DeactivationReason { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public static DeviceToken Register(DeviceTokenId id, Guid userId, string ciphertext, string hash, string mask, PushPlatform platform, PushProvider provider, DateTime now) => new(id, userId, ciphertext, hash, mask, platform, provider, now);
    public void Replace(string ciphertext, string hash, string mask, DateTime now) { if (!IsActive) throw new DomainException("Inactive token cannot be refreshed."); TokenCiphertext = NotificationRules.Required(ciphertext, nameof(ciphertext), NotificationRules.TokenMaximumLength); TokenHash = NotificationRules.Required(hash, nameof(hash), 64); TokenMask = NotificationRules.Required(mask, nameof(mask), 32); Touch(now); }
    public bool Deactivate(string reason, DateTime now) { if (!IsActive) return false; IsActive = false; DeactivatedAtUtc = now; DeactivationReason = NotificationRules.Required(reason, nameof(reason), 80); Touch(now); return true; }
    private void Touch(DateTime now) { NotificationRules.RequireUtc(now); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
}

public sealed class NotificationTemplate : AggregateRoot<NotificationTemplateId>
{
    private NotificationTemplate() : base(default) { }
    private NotificationTemplate(NotificationTemplateId id, string key, NotificationChannel channel, string language, string? subject, string body, bool active, DateTime now) : base(id) { Key = NotificationRules.Required(key, nameof(key), NotificationRules.TemplateKeyMaximumLength); Channel = channel; Language = NotificationRules.Required(language, nameof(language), 10); Subject = subject; Body = NotificationRules.Required(body, nameof(body), NotificationRules.BodyMaximumLength); IsActive = active; CreatedAtUtc = UpdatedAtUtc = now; }
    public string Key { get; private set; } = string.Empty; public NotificationChannel Channel { get; private set; }
    public string Language { get; private set; } = string.Empty; public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty; public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public static NotificationTemplate Create(NotificationTemplateId id, string key, NotificationChannel channel, string language, string? subject, string body, DateTime now) => new(id, key, channel, language, subject, body, true, now);
}

public sealed class NotificationPreference : Entity<Guid>
{
    private NotificationPreference() : base(default) { }
    private NotificationPreference(Guid id, Guid userId, string category, NotificationChannel channel, bool enabled, DateTime now) : base(id) { if (userId == Guid.Empty) throw new DomainException("Preference owner is required."); UserId = userId; Category = NotificationRules.Required(category, nameof(category), NotificationRules.CategoryMaximumLength); Channel = channel; Enabled = enabled; UpdatedAtUtc = now; }
    public Guid UserId { get; private set; }
    public string Category { get; private set; } = string.Empty; public NotificationChannel Channel { get; private set; }
    public bool Enabled { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public static NotificationPreference Create(Guid userId, string category, NotificationChannel channel, bool enabled, DateTime now) => new(Guid.NewGuid(), userId, category, channel, enabled, now);
    public void Set(bool enabled, DateTime now) { NotificationRules.RequireUtc(now); Enabled = enabled; UpdatedAtUtc = now; }
}
