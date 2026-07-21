using System.Net;
using System.Text.RegularExpressions;
using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public sealed class PasswordHistory : Entity<PasswordHistoryId>
{
    private PasswordHistory(PasswordHistoryId id, UserId userId, PasswordHash passwordHash, DateTime becameActiveUtc, DateTime? replacedUtc, DateTime createdUtc) : base(id) { UserId = userId; PasswordHash = passwordHash; BecameActiveUtc = becameActiveUtc; ReplacedUtc = replacedUtc; CreatedUtc = createdUtc; }
    public UserId UserId { get; }
    public PasswordHash PasswordHash { get; }
    public DateTime BecameActiveUtc { get; }
    public DateTime? ReplacedUtc { get; }
    public DateTime CreatedUtc { get; }
    public static PasswordHistory Create(PasswordHistoryId id, UserId userId, PasswordHash passwordHash, DateTime becameActiveUtc, DateTime? replacedUtc, DateTime createdUtc)
    {
        DomainRules.RequireUtc(becameActiveUtc, nameof(becameActiveUtc)); DomainRules.RequireUtc(createdUtc, nameof(createdUtc)); if (replacedUtc is not null) { DomainRules.RequireUtc(replacedUtc.Value, nameof(replacedUtc)); if (replacedUtc < becameActiveUtc) throw new DomainException("Password replacement cannot precede activation."); }
        return new PasswordHistory(id, userId, passwordHash, becameActiveUtc, replacedUtc, createdUtc);
    }
}

public sealed class LoginHistory : Entity<LoginHistoryId>
{
    private static readonly Regex HashPattern = new(@"^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);
    private LoginHistory(LoginHistoryId id, UserId? userId, DeviceId? deviceId, LoginSessionId? loginSessionId, string attemptedIdentifierHash, LoginResult result, LoginFailureReason? failureReason, IPAddress? ipAddress, string? userAgent, DateTime occurredUtc, string correlationId) : base(id)
    { UserId = userId; DeviceId = deviceId; LoginSessionId = loginSessionId; AttemptedIdentifierHash = attemptedIdentifierHash.ToLowerInvariant(); Result = result; FailureReason = failureReason; IpAddress = ipAddress; UserAgent = userAgent; OccurredUtc = occurredUtc; CorrelationId = correlationId; }
    public UserId? UserId { get; }
    public DeviceId? DeviceId { get; }
    public LoginSessionId? LoginSessionId { get; }
    public string AttemptedIdentifierHash { get; }
    public LoginResult Result { get; }
    public LoginFailureReason? FailureReason { get; }
    public IPAddress? IpAddress { get; }
    public string? UserAgent { get; }
    public DateTime OccurredUtc { get; }
    public string CorrelationId { get; }
    public static LoginHistory Create(LoginHistoryId id, UserId? userId, DeviceId? deviceId, LoginSessionId? loginSessionId, string attemptedIdentifierHash, LoginResult result, LoginFailureReason? failureReason, IPAddress? ipAddress, string? userAgent, DateTime occurredUtc, string correlationId)
    {
        DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc)); if (!Enum.IsDefined(result)) throw new DomainException("Login result is invalid."); if (attemptedIdentifierHash is null || !HashPattern.IsMatch(attemptedIdentifierHash)) throw new DomainException("Attempted identifier must be stored as a SHA-256 hash.");
        if (result == LoginResult.Succeeded && failureReason is not null) throw new DomainException("Successful login history cannot have a failure reason.");
        if (result == LoginResult.Failed && (failureReason is null || loginSessionId is not null)) throw new DomainException("Failed login history requires a reason and cannot reference a session.");
        return new LoginHistory(id, userId, deviceId, loginSessionId, attemptedIdentifierHash, result, failureReason, ipAddress, DomainRules.Optional(userAgent, 1024, nameof(userAgent)), occurredUtc, DomainRules.Required(correlationId, 128, nameof(correlationId)));
    }
}
