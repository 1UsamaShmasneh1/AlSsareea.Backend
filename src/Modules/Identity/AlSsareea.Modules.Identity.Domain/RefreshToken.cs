using System.Net;
using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public sealed class RefreshToken : Entity<RefreshTokenId>
{
    private RefreshToken(RefreshTokenId id, UserId userId, DeviceId? deviceId, LoginSessionId loginSessionId, RefreshTokenHash tokenHash, Guid securityStampSnapshot, DateTime createdUtc, DateTime expiresUtc, IPAddress? createdByIpAddress) : base(id)
    {
        UserId = userId; DeviceId = deviceId; LoginSessionId = loginSessionId; TokenHash = tokenHash; SecurityStampSnapshot = securityStampSnapshot; CreatedUtc = createdUtc; ExpiresUtc = expiresUtc; CreatedByIpAddress = createdByIpAddress;
    }
    public UserId UserId { get; private set; }
    public DeviceId? DeviceId { get; private set; }
    public LoginSessionId LoginSessionId { get; private set; }
    public RefreshTokenHash TokenHash { get; private set; }
    public Guid SecurityStampSnapshot { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime ExpiresUtc { get; private set; }
    public DateTime? ConsumedUtc { get; private set; }
    public DateTime? RevokedUtc { get; private set; }
    public string? RevocationReason { get; private set; }
    public RefreshTokenId? ReplacedByTokenId { get; private set; }
    public IPAddress? CreatedByIpAddress { get; private set; }
    public IPAddress? RevokedByIpAddress { get; private set; }
    public bool IsActive(DateTime utcNow) => ConsumedUtc is null && RevokedUtc is null && utcNow < ExpiresUtc;
    public static RefreshToken Create(RefreshTokenId id, UserId userId, DeviceId? deviceId, LoginSessionId sessionId, RefreshTokenHash hash, Guid securityStampSnapshot, DateTime createdUtc, DateTime expiresUtc, IPAddress? createdByIpAddress = null)
    {
        DomainRules.RequireUtc(createdUtc, nameof(createdUtc)); DomainRules.RequireUtc(expiresUtc, nameof(expiresUtc)); if (expiresUtc <= createdUtc) throw new DomainException("Refresh token expiry must be after creation time."); if (securityStampSnapshot == Guid.Empty) throw new DomainException("Security stamp snapshot is required.");
        return new RefreshToken(id, userId, deviceId, sessionId, hash, securityStampSnapshot, createdUtc, expiresUtc, createdByIpAddress);
    }
    public void Consume(DateTime occurredUtc) { EnsureActive(occurredUtc); ConsumedUtc = occurredUtc; }
    public void Revoke(string reason, DateTime occurredUtc, IPAddress? revokedByIpAddress = null) { EnsureActive(occurredUtc); RevocationReason = DomainRules.Required(reason, 250, nameof(reason)); RevokedUtc = occurredUtc; RevokedByIpAddress = revokedByIpAddress; }
    public void Replace(RefreshTokenId replacementId, DateTime occurredUtc) { if (replacementId == Id) throw new DomainException("Refresh token cannot replace itself."); EnsureActive(occurredUtc); ReplacedByTokenId = replacementId; ConsumedUtc = occurredUtc; }
    private void EnsureActive(DateTime occurredUtc) { DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc)); if (!IsActive(occurredUtc)) throw new DomainException("Refresh token is not active."); }
}
