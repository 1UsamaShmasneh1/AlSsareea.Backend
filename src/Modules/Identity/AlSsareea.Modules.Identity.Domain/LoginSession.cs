using System.Net;
using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public sealed class LoginSession : Entity<LoginSessionId>
{
    private LoginSession(LoginSessionId id, UserId userId, DeviceId? deviceId, DateTime startedUtc, DateTime expiresUtc, IPAddress? ipAddress, string? userAgent) : base(id)
    {
        UserId = userId; DeviceId = deviceId; State = SessionState.Active; StartedUtc = startedUtc; ExpiresUtc = expiresUtc; LastActivityUtc = startedUtc; IpAddress = ipAddress; UserAgent = userAgent; CreatedUtc = startedUtc; UpdatedUtc = startedUtc;
    }
    public UserId UserId { get; private set; }
    public DeviceId? DeviceId { get; private set; }
    public RefreshTokenId? RefreshTokenId { get; private set; }
    public SessionState State { get; private set; }
    public DateTime StartedUtc { get; private set; }
    public DateTime ExpiresUtc { get; private set; }
    public DateTime LastActivityUtc { get; private set; }
    public DateTime? EndedUtc { get; private set; }
    public SessionEndReason? EndReason { get; private set; }
    public IPAddress? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }
    public static LoginSession Start(LoginSessionId id, UserId userId, DeviceId? deviceId, DateTime startedUtc, DateTime expiresUtc, IPAddress? ipAddress = null, string? userAgent = null)
    {
        DomainRules.RequireUtc(startedUtc, nameof(startedUtc)); DomainRules.RequireUtc(expiresUtc, nameof(expiresUtc));
        if (expiresUtc <= startedUtc) throw new DomainException("Session expiry must be after its start time.");
        return new LoginSession(id, userId, deviceId, startedUtc, expiresUtc, ipAddress, DomainRules.Optional(userAgent, 1024, nameof(userAgent)));
    }
    public void AttachRefreshToken(RefreshTokenId tokenId, DateTime occurredUtc) { EnsureActive(); RefreshTokenId = tokenId; UpdateActivity(occurredUtc); }
    public void UpdateActivity(DateTime occurredUtc) { EnsureActive(); DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc)); if (occurredUtc < StartedUtc || occurredUtc < LastActivityUtc || occurredUtc > ExpiresUtc) throw new DomainException("Session activity time is invalid."); LastActivityUtc = occurredUtc; UpdatedUtc = occurredUtc; }
    public void Expire(DateTime occurredUtc) => End(SessionState.Expired, SessionEndReason.Expired, occurredUtc);
    public void Revoke(SessionEndReason reason, DateTime occurredUtc) { if (reason is SessionEndReason.UserLogout or SessionEndReason.Expired) throw new DomainException("Revocation reason is invalid."); End(SessionState.Revoked, reason, occurredUtc); }
    public void Logout(DateTime occurredUtc) => End(SessionState.LoggedOut, SessionEndReason.UserLogout, occurredUtc);
    private void End(SessionState state, SessionEndReason reason, DateTime occurredUtc)
    {
        EnsureActive(); DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc)); if (occurredUtc < StartedUtc) throw new DomainException("Session end time cannot precede start time."); State = state; EndReason = reason; EndedUtc = occurredUtc; UpdatedUtc = occurredUtc;
    }
    private void EnsureActive() { if (State != SessionState.Active) throw new DomainException("Session is no longer active."); }
}
