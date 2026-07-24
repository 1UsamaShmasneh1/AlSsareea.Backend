using System.Net;
using System.Security.Cryptography;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using AlSsareea.Modules.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Identity.Infrastructure.Authentication;

internal sealed partial class AuthenticationService(
    IdentityDbContext db,
    IClock clock,
    IPasswordHasher passwordHasher,
    IOtpGenerator otpGenerator,
    IOtpHasher otpHasher,
    IOtpDeliveryProvider otpDeliveryProvider,
    TokenGenerator tokenGenerator,
    IOptions<AuthenticationOptions> authenticationOptions,
    IOptions<LockoutOptions> lockoutOptions,
    IOptions<OtpOptions> otpOptions,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private readonly AuthenticationOptions _authentication = authenticationOptions.Value;
    private readonly LockoutOptions _lockout = lockoutOptions.Value;
    private readonly OtpOptions _otp = otpOptions.Value;
    private readonly string _dummyHash = passwordHasher.Hash("constant-time-dummy-password").EncodedHash;

    public async Task<AuthenticationResult<TokenResponse>> LoginAsync(LoginRequest request, AuthenticationRequestContext context, CancellationToken cancellationToken)
    {
        string? normalizedIdentifier = NormalizeIdentifier(request.Identifier);
        if (normalizedIdentifier is null || !IsPasswordInputValid(request.Password) || request.Device is null)
            return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.InvalidCredentials, 401);

        User? user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedIdentifier || x.NormalizedPhoneNumber == normalizedIdentifier, cancellationToken);
        if (user is null)
        {
            _ = passwordHasher.Verify(request.Password, _dummyHash);
            db.LoginHistory.Add(CreateLoginHistory(null, null, null, normalizedIdentifier, LoginResult.Failed, LoginFailureReason.InvalidCredentials, context));
            await db.SaveChangesAsync(cancellationToken);
            return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.InvalidCredentials, 401);
        }

        DateTime now = clock.UtcNow;
        if (user.UnlockIfExpired(now))
        {
            AddAudit("account.unlocked", user.Id, null, null, context, "automatic");
            await db.SaveChangesAsync(cancellationToken);
        }
        if (user.Status != UserStatus.Active)
        {
            db.LoginHistory.Add(CreateLoginHistory(user.Id, null, null, normalizedIdentifier, LoginResult.Failed, MapFailure(user.Status), context));
            await db.SaveChangesAsync(cancellationToken);
            return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.AccountUnavailable, 401);
        }

        PasswordVerificationResult verification = passwordHasher.Verify(request.Password, user.PasswordHash.Value);
        if (verification == PasswordVerificationResult.Failed)
        {
            DateTime lockoutEnd = now.AddMinutes(_lockout.LockoutMinutes);
            Guid concurrencyStamp = Guid.NewGuid();
            await db.Users.Where(x => x.Id == user.Id && x.Status == UserStatus.Active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.FailedLoginCount, x => x.FailedLoginCount + 1)
                    .SetProperty(x => x.LockoutEndUtc, x => x.FailedLoginCount + 1 >= _lockout.MaximumFailedAttempts ? lockoutEnd : x.LockoutEndUtc)
                    .SetProperty(x => x.Status, x => x.FailedLoginCount + 1 >= _lockout.MaximumFailedAttempts ? UserStatus.Locked : x.Status)
                    .SetProperty(x => x.ConcurrencyStamp, concurrencyStamp)
                    .SetProperty(x => x.UpdatedUtc, now), cancellationToken);
            db.Entry(user).State = EntityState.Detached;
            bool locked = await db.Users.AsNoTracking().Where(x => x.Id == user.Id).Select(x => x.Status == UserStatus.Locked).SingleOrDefaultAsync(cancellationToken);
            db.LoginHistory.Add(CreateLoginHistory(user.Id, null, null, normalizedIdentifier, LoginResult.Failed, LoginFailureReason.InvalidCredentials, context));
            if (locked) AddAudit("account.locked", user.Id, null, null, context, "failed_attempt_limit");
            await db.SaveChangesAsync(cancellationToken);
            return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.InvalidCredentials, 401);
        }

        user.ResetFailedLogins(now);
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.ChangePassword(new PasswordHash(passwordHasher.Hash(request.Password).EncodedHash), now);
            AddAudit("password_hash.upgraded", user.Id, null, null, context, "success");
        }

        DeviceIdentifier deviceIdentifier;
        try { deviceIdentifier = new DeviceIdentifier(request.Device.DeviceIdentifier); }
        catch (DomainException) { return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.InvalidCredentials, 400); }
        Device? device = await db.Devices.SingleOrDefaultAsync(x => x.UserId == user.Id && x.DeviceIdentifier == deviceIdentifier, cancellationToken);
        if (device is null)
        {
            device = Device.Register(DeviceId.New(), user.Id, deviceIdentifier, request.Device.Platform, request.Device.DeviceName, request.Device.AppVersion, request.Device.OperatingSystemVersion, now);
            db.Devices.Add(device);
        }
        else
        {
            if (device.IsRevoked) return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.AccountUnavailable, 401);
            device.UpdateLastSeen(now);
        }

        var session = LoginSession.Start(LoginSessionId.New(), user.Id, device.Id, now, now.AddDays(_authentication.SessionDays), ParseIp(context.IpAddress), context.UserAgent);
        GeneratedRefreshToken generatedRefresh = TokenGenerator.GenerateRefreshToken();
        Guid familyId = Guid.NewGuid();
        var refreshToken = RefreshToken.Create(RefreshTokenId.New(), user.Id, device.Id, session.Id, generatedRefresh.Hash, user.SecurityStamp, now, now.AddDays(_authentication.RefreshTokenDays), ParseIp(context.IpAddress), familyId);
        db.LoginSessions.Add(session); db.RefreshTokens.Add(refreshToken);
        db.LoginHistory.Add(CreateLoginHistory(user.Id, device.Id, session.Id, normalizedIdentifier, LoginResult.Succeeded, null, context));
        AddAudit("session.created", user.Id, session.Id, device.Id, context, "success");
        await db.SaveChangesAsync(cancellationToken);
        (string[] roles, string[] permissions) = await LoadAuthorizationAsync(user.Id, cancellationToken);
        GeneratedAccessToken access = tokenGenerator.GenerateAccessToken(user, session.Id, roles, permissions, now);
        LogAuthenticationSucceeded(logger, user.Id.Value, session.Id.Value);
        return AuthenticationResult<TokenResponse>.Success(new TokenResponse("Bearer", access.Token, access.ExpiresInSeconds, generatedRefresh.RawToken, refreshToken.ExpiresUtc, session.Id.Value, new AuthenticatedUserResponse(user.Id.Value, user.UserType.ToString())));
    }

    public async Task<AuthenticationResult<TokenResponse>> RefreshAsync(RefreshRequest request, AuthenticationRequestContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.RefreshTokenInvalid, 401);
        DateTime now = clock.UtcNow; RefreshTokenHash hash;
        try { hash = new RefreshTokenHash(TokenGenerator.Sha256(request.RefreshToken)); }
        catch (DomainException) { return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.RefreshTokenInvalid, 401); }
        RefreshToken? existing = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (existing is null) return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.RefreshTokenInvalid, 401);
        User? user = await db.Users.SingleOrDefaultAsync(x => x.Id == existing.UserId, cancellationToken);
        LoginSession? session = await db.LoginSessions.SingleOrDefaultAsync(x => x.Id == existing.LoginSessionId, cancellationToken);
        Device? device = existing.DeviceId is null ? null : await db.Devices.SingleOrDefaultAsync(x => x.Id == existing.DeviceId, cancellationToken);
        bool deviceMatches = device is not null && FixedEquals(device.DeviceIdentifier.Value, request.DeviceIdentifier);
        if (!existing.IsActive(now) || user?.Status != UserStatus.Active || session?.State != SessionState.Active || !deviceMatches || existing.SecurityStampSnapshot != user.SecurityStamp)
        {
            await HandleReplayAsync(existing, session, context, now, cancellationToken);
            return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.RefreshTokenInvalid, 401);
        }

        GeneratedRefreshToken next = TokenGenerator.GenerateRefreshToken(); RefreshTokenId nextId = RefreshTokenId.New();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var replacement = RefreshToken.Create(nextId, existing.UserId, existing.DeviceId, existing.LoginSessionId, next.Hash, user.SecurityStamp, now, now.AddDays(_authentication.RefreshTokenDays), ParseIp(context.IpAddress), existing.FamilyId ?? Guid.NewGuid());
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        int updated = await db.RefreshTokens.Where(x => x.Id == existing.Id && x.ConsumedUtc == null && x.RevokedUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ConsumedUtc, now).SetProperty(x => x.ReplacedByTokenId, nextId), cancellationToken);
        if (updated != 1)
        {
            await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); session = await db.LoginSessions.SingleOrDefaultAsync(x => x.Id == existing.LoginSessionId, cancellationToken); await HandleReplayAsync(existing, session, context, now, cancellationToken);
            return AuthenticationResult<TokenResponse>.Failure(AuthenticationErrorCodes.RefreshTokenInvalid, 401);
        }
        session.UpdateActivity(now); AddAudit("refresh_token.rotated", user.Id, session.Id, device?.Id, context, "success"); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        (string[] roles, string[] permissions) = await LoadAuthorizationAsync(user.Id, cancellationToken); GeneratedAccessToken access = tokenGenerator.GenerateAccessToken(user, session.Id, roles, permissions, now);
        return AuthenticationResult<TokenResponse>.Success(new TokenResponse("Bearer", access.Token, access.ExpiresInSeconds, next.RawToken, replacement.ExpiresUtc, session.Id.Value, new AuthenticatedUserResponse(user.Id.Value, user.UserType.ToString())));
    }

    public async Task<AuthenticationResult<CurrentUserResponse>> GetCurrentUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        User? user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken); if (user is null) return AuthenticationResult<CurrentUserResponse>.Failure(AuthenticationErrorCodes.SessionInvalid, 401);
        (string[] roles, string[] permissions) = await LoadAuthorizationAsync(userId, cancellationToken); return AuthenticationResult<CurrentUserResponse>.Success(new CurrentUserResponse(user.Id.Value, user.UserType.ToString(), roles.ToHashSet(StringComparer.Ordinal), permissions.ToHashSet(StringComparer.Ordinal)));
    }

    public async Task<IReadOnlyList<SessionResponse>> GetSessionsAsync(UserId userId, LoginSessionId currentSessionId, CancellationToken cancellationToken) =>
        await (from session in db.LoginSessions.AsNoTracking() join device in db.Devices.AsNoTracking() on session.DeviceId equals device.Id into devices from device in devices.DefaultIfEmpty() where session.UserId == userId orderby session.StartedUtc descending select new SessionResponse(session.Id.Value, device == null ? null : device.DeviceName, device == null ? null : device.Platform.ToString(), session.StartedUtc, session.LastActivityUtc, session.State.ToString(), session.Id == currentSessionId)).ToListAsync(cancellationToken);

    public Task<AuthenticationResult<bool>> RevokeSessionAsync(UserId userId, LoginSessionId sessionId, string idempotencyKey, CancellationToken cancellationToken) => RevokeSessionCoreAsync(userId, sessionId, "session.revoke", idempotencyKey, false, cancellationToken);
    public Task<AuthenticationResult<bool>> LogoutAsync(UserId userId, LoginSessionId sessionId, string idempotencyKey, CancellationToken cancellationToken) => RevokeSessionCoreAsync(userId, sessionId, "session.logout", idempotencyKey, true, cancellationToken);

    public async Task<AuthenticationResult<bool>> LogoutAllAsync(UserId userId, string idempotencyKey, CancellationToken cancellationToken)
    {
        IdempotencyOutcome outcome = await EnsureIdempotencyAsync(userId.Value.ToString(), "session.logout_all", idempotencyKey, "none", null, cancellationToken); if (outcome == IdempotencyOutcome.Conflict) return AuthenticationResult<bool>.Failure(AuthenticationErrorCodes.IdempotencyConflict, 409); if (outcome == IdempotencyOutcome.Duplicate) return AuthenticationResult<bool>.Success(true);
        DateTime now = clock.UtcNow; List<LoginSession> sessions = await db.LoginSessions.Where(x => x.UserId == userId && x.State == SessionState.Active).ToListAsync(cancellationToken); foreach (LoginSession session in sessions) session.Revoke(SessionEndReason.SecurityStampChanged, now);
        await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedUtc == null && x.ConsumedUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedUtc, now).SetProperty(x => x.RevocationReason, "logout_all"), cancellationToken);
        User? user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken); user?.RotateSecurityStampForSessionRevocation(now); AddAudit("sessions.revoked_all", userId, null, null, NewInternalContext(), "success"); await db.SaveChangesAsync(cancellationToken); return AuthenticationResult<bool>.Success(true);
    }

    public async Task<AuthenticationResult<OtpChallengeResponse>> CreateOtpChallengeAsync(OtpChallengeRequest request, string ownerKey, string idempotencyKey, AuthenticationRequestContext context, CancellationToken cancellationToken)
    {
        if (!otpDeliveryProvider.IsAvailable) return AuthenticationResult<OtpChallengeResponse>.Failure(AuthenticationErrorCodes.OtpDeliveryUnavailable, 503);
        string? normalized = NormalizeIdentifier(request.Destination); if (normalized is null) return AuthenticationResult<OtpChallengeResponse>.Failure(AuthenticationErrorCodes.OtpInvalid, 400);
        string destinationHash = TokenGenerator.Sha256(normalized); string fingerprint = TokenGenerator.Sha256(destinationHash + ":" + request.Purpose + ":" + request.DeviceIdentifier);
        IdempotencyRecord? existingRecord = await FindIdempotencyAsync(ownerKey, "otp.create", idempotencyKey, cancellationToken);
        if (existingRecord is not null)
        {
            if (!FixedEquals(existingRecord.RequestFingerprint, fingerprint)) return AuthenticationResult<OtpChallengeResponse>.Failure(AuthenticationErrorCodes.IdempotencyConflict, 409);
            if (Guid.TryParse(existingRecord.ResourceId, out Guid existingId)) { OtpChallenge? prior = await db.OtpChallenges.AsNoTracking().SingleOrDefaultAsync(x => x.Id == new OtpChallengeId(existingId), cancellationToken); if (prior is not null) return AuthenticationResult<OtpChallengeResponse>.Success(new OtpChallengeResponse(existingId, prior.ExpiresUtc, prior.NextResendUtc, null)); }
        }
        DateTime now = clock.UtcNow; OtpChallenge? recent = await db.OtpChallenges.AsNoTracking().Where(x => x.DestinationHash == destinationHash && x.Purpose == request.Purpose).OrderByDescending(x => x.CreatedUtc).FirstOrDefaultAsync(cancellationToken); if (recent is not null && recent.NextResendUtc > now) return AuthenticationResult<OtpChallengeResponse>.Failure("auth.otp_resend_blocked", 429);
        OtpChallengeId id = OtpChallengeId.New(); string code = otpGenerator.Generate().Code; string codeHash = otpHasher.Hash(code, id);
        var challenge = OtpChallenge.Create(id, destinationHash, request.Purpose, codeHash, _otp.MaximumAttempts, now, now.AddMinutes(_otp.LifetimeMinutes), now.AddSeconds(_otp.ResendSeconds), TokenGenerator.Sha256(request.DeviceIdentifier), context.IpAddress is null ? null : TokenGenerator.Sha256(context.IpAddress)); db.OtpChallenges.Add(challenge);
        db.IdempotencyRecords.Add(IdempotencyRecord.Create(IdempotencyRecordId.New(), ownerKey, "otp.create", TokenGenerator.Sha256(idempotencyKey), fingerprint, id.Value.ToString(), now, now.AddHours(24))); AddAudit("otp.generated", null, null, null, context, request.Purpose.ToString()); await db.SaveChangesAsync(cancellationToken); await otpDeliveryProvider.SendAsync(normalized, request.Purpose, code, cancellationToken);
        return AuthenticationResult<OtpChallengeResponse>.Success(new OtpChallengeResponse(id.Value, challenge.ExpiresUtc, challenge.NextResendUtc, _otp.DevelopmentProviderEnabled ? code : null));
    }

    public async Task<AuthenticationResult<bool>> VerifyOtpAsync(OtpChallengeId challengeId, OtpVerifyRequest request, CancellationToken cancellationToken)
    {
        DateTime now = clock.UtcNow; OtpChallenge? challenge = await db.OtpChallenges.AsNoTracking().SingleOrDefaultAsync(x => x.Id == challengeId, cancellationToken); if (challenge is null) return AuthenticationResult<bool>.Failure(AuthenticationErrorCodes.OtpInvalid, 401); if (challenge.IsExpired(now)) return AuthenticationResult<bool>.Failure(AuthenticationErrorCodes.OtpExpired, 401);
        string deviceHash = TokenGenerator.Sha256(request.DeviceIdentifier);
        bool codeMatches = otpHasher.Verify(request.Code, challengeId, challenge.CodeHash);
        int used = codeMatches ? await db.OtpChallenges.Where(x => x.Id == challengeId && x.UsedUtc == null && x.ExpiresUtc > now && x.FailedAttempts < x.MaximumAttempts && x.DeviceIdentifierHash == deviceHash).ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedUtc, now), cancellationToken) : 0;
        if (used == 1) { AddAudit("otp.verification_succeeded", null, null, null, NewInternalContext(), challenge.Purpose.ToString()); await db.SaveChangesAsync(cancellationToken); return AuthenticationResult<bool>.Success(true); }
        int failed = await db.OtpChallenges.Where(x => x.Id == challengeId && x.UsedUtc == null && x.ExpiresUtc > now && x.FailedAttempts < x.MaximumAttempts).ExecuteUpdateAsync(s => s.SetProperty(x => x.FailedAttempts, x => x.FailedAttempts + 1), cancellationToken); db.ChangeTracker.Clear(); OtpChallenge current = await db.OtpChallenges.AsNoTracking().SingleAsync(x => x.Id == challengeId, cancellationToken); AddAudit("otp.verification_failed", null, null, null, NewInternalContext(), "invalid"); await db.SaveChangesAsync(cancellationToken);
        return failed == 0 || current.FailedAttempts >= current.MaximumAttempts ? AuthenticationResult<bool>.Failure(AuthenticationErrorCodes.OtpAttemptsExceeded, 401) : AuthenticationResult<bool>.Failure(AuthenticationErrorCodes.OtpInvalid, 401);
    }

    private async Task<AuthenticationResult<bool>> RevokeSessionCoreAsync(UserId userId, LoginSessionId sessionId, string operation, string key, bool logout, CancellationToken ct)
    {
        IdempotencyOutcome outcome = await EnsureIdempotencyAsync(userId.Value.ToString(), operation, key, sessionId.Value.ToString(), null, ct); if (outcome == IdempotencyOutcome.Conflict) return AuthenticationResult<bool>.Failure(AuthenticationErrorCodes.IdempotencyConflict, 409); if (outcome == IdempotencyOutcome.Duplicate) return AuthenticationResult<bool>.Success(true);
        LoginSession? session = await db.LoginSessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct); if (session is null) return AuthenticationResult<bool>.Failure(AuthenticationErrorCodes.Forbidden, 403); DateTime now = clock.UtcNow; if (session.State == SessionState.Active) { if (logout) session.Logout(now); else session.Revoke(SessionEndReason.AdministratorRevoked, now); }
        await db.RefreshTokens.Where(x => x.LoginSessionId == sessionId && x.RevokedUtc == null && x.ConsumedUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedUtc, now).SetProperty(x => x.RevocationReason, operation), ct); AddAudit("session.revoked", userId, sessionId, session.DeviceId, NewInternalContext(), operation); await db.SaveChangesAsync(ct); return AuthenticationResult<bool>.Success(true);
    }

    private async Task HandleReplayAsync(RefreshToken token, LoginSession? session, AuthenticationRequestContext context, DateTime now, CancellationToken ct)
    {
        await db.RefreshTokens.Where(x => (token.FamilyId != null && x.FamilyId == token.FamilyId || x.LoginSessionId == token.LoginSessionId) && x.RevokedUtc == null && x.ConsumedUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedUtc, now).SetProperty(x => x.RevocationReason, "replay_detected"), ct);
        await db.RefreshTokens.Where(x => x.Id == token.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReplayDetectedUtc, now), ct); if (session?.State == SessionState.Active) session.Revoke(SessionEndReason.RefreshRevoked, now); AddAudit("refresh_token.replay_detected", token.UserId, token.LoginSessionId, token.DeviceId, context, "rejected"); await db.SaveChangesAsync(ct);
    }

    private async Task<(string[] Roles, string[] Permissions)> LoadAuthorizationAsync(UserId userId, CancellationToken ct)
    {
        string[] roles = await (from ur in db.UserRoles.AsNoTracking() join role in db.Roles.AsNoTracking() on ur.RoleId equals role.Id where ur.UserId == userId && role.IsActive select role.NormalizedName).Distinct().ToArrayAsync(ct);
        string[] permissions = await (from ur in db.UserRoles.AsNoTracking() join role in db.Roles.AsNoTracking() on ur.RoleId equals role.Id join rp in db.RolePermissions.AsNoTracking() on role.Id equals rp.RoleId join permission in db.Permissions.AsNoTracking() on rp.PermissionId equals permission.Id where ur.UserId == userId && role.IsActive && permission.IsActive select permission.NormalizedName).Distinct().ToArrayAsync(ct);
        return (roles, permissions);
    }

    private async Task<IdempotencyOutcome> EnsureIdempotencyAsync(string owner, string operation, string key, string fingerprintSource, string? resourceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) return IdempotencyOutcome.Conflict; string fingerprint = TokenGenerator.Sha256(fingerprintSource); IdempotencyRecord? existing = await FindIdempotencyAsync(owner, operation, key, ct); if (existing is not null) return FixedEquals(existing.RequestFingerprint, fingerprint) ? IdempotencyOutcome.Duplicate : IdempotencyOutcome.Conflict;
        DateTime now = clock.UtcNow; db.IdempotencyRecords.Add(IdempotencyRecord.Create(IdempotencyRecordId.New(), owner, operation, TokenGenerator.Sha256(key), fingerprint, resourceId, now, now.AddHours(24))); return IdempotencyOutcome.New;
    }

    private Task<IdempotencyRecord?> FindIdempotencyAsync(string owner, string operation, string key, CancellationToken ct) { string keyHash = TokenGenerator.Sha256(key); return db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerKey == owner && x.Operation == operation && x.KeyHash == keyHash && x.ExpiresUtc > clock.UtcNow, ct); }
    private static string? NormalizeIdentifier(string identifier) { try { return identifier.Contains('@', StringComparison.Ordinal) ? new Email(identifier).Normalized : new PhoneNumber(identifier).Normalized; } catch (DomainException) { return null; } }
    private static bool IsPasswordInputValid(string password) => password is not null && password.Length is >= 10 and <= 128 && !string.IsNullOrWhiteSpace(password);
    private static LoginFailureReason MapFailure(UserStatus status) => status switch { UserStatus.Locked => LoginFailureReason.LockedOut, UserStatus.Suspended => LoginFailureReason.Suspended, UserStatus.Disabled or UserStatus.Deleted => LoginFailureReason.Disabled, _ => LoginFailureReason.Unknown };
    private static IPAddress? ParseIp(string? value) => IPAddress.TryParse(value, out IPAddress? address) ? address : null;
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(left), System.Text.Encoding.UTF8.GetBytes(right));
    private LoginHistory CreateLoginHistory(UserId? userId, DeviceId? deviceId, LoginSessionId? sessionId, string identifier, LoginResult result, LoginFailureReason? failure, AuthenticationRequestContext context) => LoginHistory.Create(LoginHistoryId.New(), userId, deviceId, sessionId, TokenGenerator.Sha256(identifier), result, failure, ParseIp(context.IpAddress), context.UserAgent, clock.UtcNow, context.CorrelationId);
    private void AddAudit(string eventType, UserId? userId, LoginSessionId? sessionId, DeviceId? deviceId, AuthenticationRequestContext context, string? category) => db.SecurityAuditRecords.Add(SecurityAuditRecord.Create(SecurityAuditRecordId.New(), eventType, userId, sessionId, deviceId, clock.UtcNow, context.CorrelationId, context.IpAddress, context.UserAgent, category));
    private static AuthenticationRequestContext NewInternalContext() => new(null, null, "internal-" + Guid.NewGuid().ToString("N"));
    private enum IdempotencyOutcome { New, Duplicate, Conflict }

    [LoggerMessage(LogLevel.Information, "Authentication event login.succeeded for user {UserId} and session {SessionId}")]
    private static partial void LogAuthenticationSucceeded(ILogger logger, Guid userId, Guid sessionId);
}
