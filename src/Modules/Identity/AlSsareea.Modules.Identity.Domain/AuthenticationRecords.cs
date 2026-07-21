using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public sealed class OtpChallenge : Entity<OtpChallengeId>
{
    private OtpChallenge(OtpChallengeId id, string destinationHash, OtpPurpose purpose, string codeHash, int maximumAttempts, DateTime createdUtc, DateTime expiresUtc, DateTime nextResendUtc, string deviceIdentifierHash, string? ipAddressHash) : base(id)
    {
        DestinationHash = destinationHash; Purpose = purpose; CodeHash = codeHash; MaximumAttempts = maximumAttempts; CreatedUtc = createdUtc; ExpiresUtc = expiresUtc; NextResendUtc = nextResendUtc; DeviceIdentifierHash = deviceIdentifierHash; IpAddressHash = ipAddressHash;
    }
    public string DestinationHash { get; private set; }
    public OtpPurpose Purpose { get; private set; }
    public string CodeHash { get; private set; }
    public int FailedAttempts { get; private set; }
    public int MaximumAttempts { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime ExpiresUtc { get; private set; }
    public DateTime NextResendUtc { get; private set; }
    public DateTime? UsedUtc { get; private set; }
    public string DeviceIdentifierHash { get; private set; }
    public string? IpAddressHash { get; private set; }

    public static OtpChallenge Create(OtpChallengeId id, string destinationHash, OtpPurpose purpose, string codeHash, int maximumAttempts, DateTime createdUtc, DateTime expiresUtc, DateTime nextResendUtc, string deviceIdentifierHash, string? ipAddressHash)
    {
        DomainRules.RequireUtc(createdUtc, nameof(createdUtc)); DomainRules.RequireUtc(expiresUtc, nameof(expiresUtc)); DomainRules.RequireUtc(nextResendUtc, nameof(nextResendUtc));
        if (!Enum.IsDefined(purpose) || maximumAttempts < 1 || expiresUtc <= createdUtc || nextResendUtc <= createdUtc) throw new DomainException("OTP challenge parameters are invalid.");
        ValidateHash(destinationHash); ValidateHash(codeHash); ValidateHash(deviceIdentifierHash); if (ipAddressHash is not null) ValidateHash(ipAddressHash);
        return new OtpChallenge(id, destinationHash, purpose, codeHash, maximumAttempts, createdUtc, expiresUtc, nextResendUtc, deviceIdentifierHash, ipAddressHash);
    }
    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresUtc;
    public bool CanAttempt(DateTime utcNow) => UsedUtc is null && !IsExpired(utcNow) && FailedAttempts < MaximumAttempts;
    public void RecordFailure() { if (UsedUtc is not null || FailedAttempts >= MaximumAttempts) throw new DomainException("OTP challenge cannot accept another attempt."); FailedAttempts++; }
    public void MarkUsed(DateTime utcNow) { DomainRules.RequireUtc(utcNow, nameof(utcNow)); if (!CanAttempt(utcNow)) throw new DomainException("OTP challenge is not active."); UsedUtc = utcNow; }
    private static void ValidateHash(string value) { if (value.Length != 64 || !value.All(Uri.IsHexDigit)) throw new DomainException("Security hash must be 64 hexadecimal characters."); }
}

public sealed class IdempotencyRecord : Entity<IdempotencyRecordId>
{
    private IdempotencyRecord(IdempotencyRecordId id, string ownerKey, string operation, string keyHash, string requestFingerprint, string? resourceId, DateTime createdUtc, DateTime expiresUtc) : base(id)
    { OwnerKey = ownerKey; Operation = operation; KeyHash = keyHash; RequestFingerprint = requestFingerprint; ResourceId = resourceId; CreatedUtc = createdUtc; ExpiresUtc = expiresUtc; }
    public string OwnerKey { get; private set; }
    public string Operation { get; private set; }
    public string KeyHash { get; private set; }
    public string RequestFingerprint { get; private set; }
    public string? ResourceId { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime ExpiresUtc { get; private set; }
    public static IdempotencyRecord Create(IdempotencyRecordId id, string ownerKey, string operation, string keyHash, string requestFingerprint, string? resourceId, DateTime createdUtc, DateTime expiresUtc)
    {
        DomainRules.RequireUtc(createdUtc, nameof(createdUtc)); DomainRules.RequireUtc(expiresUtc, nameof(expiresUtc)); if (expiresUtc <= createdUtc) throw new DomainException("Idempotency expiry must follow creation.");
        return new IdempotencyRecord(id, DomainRules.Required(ownerKey, 128, nameof(ownerKey)), DomainRules.Required(operation, 80, nameof(operation)), ValidateHash(keyHash), ValidateHash(requestFingerprint), DomainRules.Optional(resourceId, 128, nameof(resourceId)), createdUtc, expiresUtc);
    }
    private static string ValidateHash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit) ? value.ToLowerInvariant() : throw new DomainException("Idempotency hashes must be SHA-256 hexadecimal values.");
}

public sealed class SecurityAuditRecord : Entity<SecurityAuditRecordId>
{
    private SecurityAuditRecord(SecurityAuditRecordId id, string eventType, UserId? userId, LoginSessionId? sessionId, DeviceId? deviceId, DateTime occurredUtc, string correlationId, string? ipAddress, string? userAgent, string? resultCategory) : base(id)
    { EventType = eventType; UserId = userId; SessionId = sessionId; DeviceId = deviceId; OccurredUtc = occurredUtc; CorrelationId = correlationId; IpAddress = ipAddress; UserAgent = userAgent; ResultCategory = resultCategory; }
    public string EventType { get; private set; }
    public UserId? UserId { get; private set; }
    public LoginSessionId? SessionId { get; private set; }
    public DeviceId? DeviceId { get; private set; }
    public DateTime OccurredUtc { get; private set; }
    public string CorrelationId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? ResultCategory { get; private set; }
    public static SecurityAuditRecord Create(SecurityAuditRecordId id, string eventType, UserId? userId, LoginSessionId? sessionId, DeviceId? deviceId, DateTime occurredUtc, string correlationId, string? ipAddress, string? userAgent, string? resultCategory)
    {
        DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc));
        return new SecurityAuditRecord(id, DomainRules.Required(eventType, 80, nameof(eventType)), userId, sessionId, deviceId, occurredUtc, DomainRules.Required(correlationId, 128, nameof(correlationId)), DomainRules.Optional(ipAddress, 64, nameof(ipAddress)), DomainRules.Optional(userAgent, 512, nameof(userAgent)), DomainRules.Optional(resultCategory, 80, nameof(resultCategory)));
    }
}
