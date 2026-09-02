using AlSsareea.Modules.Identity.Domain;

namespace AlSsareea.Modules.Identity.Application;

public static class AuthenticationPolicies
{
    public const string PermissionPrefix = "Permission:";
    public const string SessionsRead = "identity.sessions.read";
    public const string SessionsRevoke = "identity.sessions.revoke";
    public const string ProfileRead = "identity.profile.read";
}

public static class AuthenticationErrorCodes
{
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string AccountUnavailable = "auth.account_unavailable";
    public const string SessionInvalid = "auth.session_invalid";
    public const string RefreshTokenInvalid = "auth.refresh_token_invalid";
    public const string OtpInvalid = "auth.otp_invalid";
    public const string OtpExpired = "auth.otp_expired";
    public const string OtpAttemptsExceeded = "auth.otp_attempts_exceeded";
    public const string OtpDeliveryUnavailable = "auth.otp_delivery_unavailable";
    public const string Forbidden = "authorization.forbidden";
    public const string IdempotencyConflict = "idempotency.key_conflict";
    public const string EmailAlreadyRegistered = "auth.email_already_registered";
    public const string ExternalTokenInvalid = "auth.external_token_invalid";
    public const string ExternalProviderUnavailable = "auth.external_provider_unavailable";
    public const string ExternalLinkRequired = "auth.external_link_required";
}

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
    public int SessionDays { get; init; } = 30;
}

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int ClockSkewSeconds { get; init; } = 30;
}

public sealed class PasswordHashingOptions
{
    public const string SectionName = "Authentication:PasswordHashing";
    public int Iterations { get; init; } = 210_000;
    public int SaltSize { get; init; } = 16;
    public int HashSize { get; init; } = 32;
}

public sealed class LockoutOptions
{
    public const string SectionName = "Authentication:Lockout";
    public int MaximumFailedAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
}

public sealed class OtpOptions
{
    public const string SectionName = "Authentication:Otp";
    public int CodeLength { get; init; } = 6;
    public int LifetimeMinutes { get; init; } = 5;
    public int MaximumAttempts { get; init; } = 5;
    public int ResendSeconds { get; init; } = 60;
    public string Pepper { get; init; } = string.Empty;
    public bool DevelopmentProviderEnabled { get; init; }
}

public sealed class AuthenticationRateLimitOptions
{
    public const string SectionName = "Authentication:RateLimit";
    public int LoginPermitLimit { get; init; } = 10;
    public int RefreshPermitLimit { get; init; } = 20;
    public int OtpPermitLimit { get; init; } = 5;
    public int RegistrationPermitLimit { get; init; } = 5;
    public int GooglePermitLimit { get; init; } = 10;
    public int WindowSeconds { get; init; } = 60;
}

public sealed class GoogleAuthenticationOptions
{
    public const string SectionName = "Authentication:Google";
    public bool Enabled { get; init; }
    public string[] AllowedClientIds { get; init; } = [];
}

public enum PasswordVerificationResult { Failed = 0, Success = 1, SuccessRehashNeeded = 2 }
public readonly record struct PasswordHashResult(string EncodedHash);

public interface IPasswordHasher
{
    PasswordHashResult Hash(string password);
    PasswordVerificationResult Verify(string password, string encodedHash);
}

public readonly record struct GeneratedOtp(string Code);

public interface IOtpGenerator
{
    GeneratedOtp Generate();
}

public interface IOtpHasher
{
    string Hash(string code, OtpChallengeId challengeId);
    bool Verify(string code, OtpChallengeId challengeId, string storedHash);
}

public interface IOtpDeliveryProvider
{
    bool IsAvailable { get; }
    Task SendAsync(string normalizedDestination, OtpPurpose purpose, string code, CancellationToken cancellationToken);
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    UserId? UserId { get; }
    LoginSessionId? SessionId { get; }
    IReadOnlySet<string> Roles { get; }
    IReadOnlySet<string> Permissions { get; }
}

public sealed record LoginDeviceRequest(string DeviceIdentifier, string? DeviceName, DevicePlatform Platform, string? AppVersion, string? OperatingSystemVersion);
public sealed record LoginRequest(string Identifier, string Password, LoginDeviceRequest Device);
public sealed record RegisterCustomerRequest(string Email, string Password, LoginDeviceRequest Device);
public sealed record GoogleAuthenticationRequest(string IdToken, string? Nonce, LoginDeviceRequest Device);
public sealed record RefreshRequest(string RefreshToken, string DeviceIdentifier);
public sealed record OtpChallengeRequest(string Destination, OtpPurpose Purpose, string DeviceIdentifier);
public sealed record OtpVerifyRequest(string Code, string DeviceIdentifier);
public sealed record TokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken, DateTime RefreshTokenExpiresUtc, Guid SessionId, AuthenticatedUserResponse User);
public sealed record GoogleAuthenticationResponse(TokenResponse Tokens, bool IsNewUser, string Email, string? GivenName, string? FamilyName);
public sealed record AuthenticatedUserResponse(Guid Id, string UserType);
public sealed record SessionResponse(Guid SessionId, string? DeviceName, string? Platform, DateTime StartedUtc, DateTime LastActivityUtc, string State, bool IsCurrent);
public sealed record CurrentUserResponse(Guid Id, string UserType, IReadOnlySet<string> Roles, IReadOnlySet<string> Permissions);
public sealed record OtpChallengeResponse(Guid ChallengeId, DateTime ExpiresUtc, DateTime NextResendUtc, string? DevelopmentCode);
public sealed record AuthenticationResult<T>(bool Succeeded, T? Value, string? ErrorCode, int StatusCode)
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Keeps strongly typed success/failure construction at the result boundary without casts.")]
    public static AuthenticationResult<T> Success(T value) => new(true, value, null, 200);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Keeps strongly typed success/failure construction at the result boundary without casts.")]
    public static AuthenticationResult<T> Failure(string code, int statusCode) => new(false, default, code, statusCode);
}

public sealed record AuthenticationRequestContext(string? IpAddress, string? UserAgent, string CorrelationId);

public sealed record GoogleIdentity(string Subject, string Email, string? GivenName, string? FamilyName);
public sealed record GoogleTokenClaims(string Subject, string Email, bool EmailVerified, string Issuer, string Audience, DateTime ExpirationUtc, string? GivenName, string? FamilyName, string? Nonce);
public sealed record GoogleTokenVerification(bool Succeeded, bool ProviderAvailable, GoogleTokenClaims? Claims);

public interface IGoogleTokenVerifier
{
    Task<GoogleTokenVerification> VerifyAsync(string idToken, CancellationToken cancellationToken);
}

public interface IGoogleIdentityValidator
{
    Task<AuthenticationResult<GoogleIdentity>> ValidateAsync(string idToken, string? nonce, CancellationToken cancellationToken);
}

public interface IAuthenticationService
{
    Task<AuthenticationResult<TokenResponse>> LoginAsync(LoginRequest request, AuthenticationRequestContext context, CancellationToken cancellationToken);
    Task<AuthenticationResult<TokenResponse>> RegisterCustomerAsync(RegisterCustomerRequest request, string idempotencyKey, AuthenticationRequestContext context, CancellationToken cancellationToken);
    Task<AuthenticationResult<GoogleAuthenticationResponse>> AuthenticateWithGoogleAsync(GoogleAuthenticationRequest request, AuthenticationRequestContext context, CancellationToken cancellationToken);
    Task<AuthenticationResult<TokenResponse>> RefreshAsync(RefreshRequest request, AuthenticationRequestContext context, CancellationToken cancellationToken);
    Task<AuthenticationResult<CurrentUserResponse>> GetCurrentUserAsync(UserId userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionResponse>> GetSessionsAsync(UserId userId, LoginSessionId currentSessionId, CancellationToken cancellationToken);
    Task<AuthenticationResult<bool>> RevokeSessionAsync(UserId userId, LoginSessionId sessionId, string idempotencyKey, CancellationToken cancellationToken);
    Task<AuthenticationResult<bool>> LogoutAsync(UserId userId, LoginSessionId sessionId, string idempotencyKey, CancellationToken cancellationToken);
    Task<AuthenticationResult<bool>> LogoutAllAsync(UserId userId, string idempotencyKey, CancellationToken cancellationToken);
    Task<AuthenticationResult<OtpChallengeResponse>> CreateOtpChallengeAsync(OtpChallengeRequest request, string ownerKey, string idempotencyKey, AuthenticationRequestContext context, CancellationToken cancellationToken);
    Task<AuthenticationResult<bool>> VerifyOtpAsync(OtpChallengeId challengeId, OtpVerifyRequest request, CancellationToken cancellationToken);
}

public interface ITokenSessionValidator
{
    Task<bool> IsValidAsync(UserId userId, LoginSessionId sessionId, Guid securityStamp, CancellationToken cancellationToken);
}
