using AlSsareea.Api.Security;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;

namespace AlSsareea.Api.Endpoints;

internal static class AuthenticationEndpoints
{
    internal static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder auth = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        auth.MapPost("/login", LoginAsync).RequireRateLimiting("auth-login").AllowAnonymous();
        auth.MapPost("/refresh", RefreshAsync).RequireRateLimiting("auth-refresh").AllowAnonymous();
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();
        auth.MapPost("/logout-all", LogoutAllAsync).RequireAuthorization();
        auth.MapGet("/me", MeAsync).RequireAuthorization();
        auth.MapGet("/sessions", SessionsAsync).RequireAuthorization(AuthenticationPolicies.PermissionPrefix + AuthenticationPolicies.SessionsRead);
        auth.MapDelete("/sessions/{sessionId:guid}", RevokeSessionAsync).RequireAuthorization(AuthenticationPolicies.PermissionPrefix + AuthenticationPolicies.SessionsRevoke);
        auth.MapPost("/otp/challenges", CreateOtpAsync).RequireRateLimiting("auth-otp").AllowAnonymous();
        auth.MapPost("/otp/challenges/{challengeId:guid}/verify", VerifyOtpAsync).RequireRateLimiting("auth-otp").AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext httpContext, IAuthenticationService service, AuthenticationRequestRateLimiter limiter, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier) || request.Password is null || request.Device is null) return Problem("auth.validation_failed", 400);
        IResult? limited = await ApplyRateLimitAsync(limiter, AuthenticationRateLimitKind.Login, httpContext, request.Identifier, request.Device.DeviceIdentifier, cancellationToken); if (limited is not null) return limited;
        AuthenticationResult<TokenResponse> result = await service.LoginAsync(request, Context(httpContext), cancellationToken); return TokenResult(result, httpContext.Response);
    }

    private static async Task<IResult> RefreshAsync(RefreshRequest request, HttpContext httpContext, IAuthenticationService service, AuthenticationRequestRateLimiter limiter, CancellationToken cancellationToken)
    {
        IResult? limited = await ApplyRateLimitAsync(limiter, AuthenticationRateLimitKind.Refresh, httpContext, TokenSubject(request.RefreshToken), request.DeviceIdentifier, cancellationToken); if (limited is not null) return limited;
        AuthenticationResult<TokenResponse> result = await service.RefreshAsync(request, Context(httpContext), cancellationToken); return TokenResult(result, httpContext.Response);
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, ICurrentUser current, IAuthenticationService service, CancellationToken cancellationToken)
    {
        if (current.UserId is null || current.SessionId is null) return Problem(AuthenticationErrorCodes.SessionInvalid, 401);
        string? key = IdempotencyKey(context); if (key is null) return Problem("idempotency.key_required", 400);
        return ToResult(await service.LogoutAsync(current.UserId.Value, current.SessionId.Value, key, cancellationToken));
    }

    private static async Task<IResult> LogoutAllAsync(HttpContext context, ICurrentUser current, IAuthenticationService service, CancellationToken cancellationToken)
    {
        if (current.UserId is null) return Problem(AuthenticationErrorCodes.SessionInvalid, 401); string? key = IdempotencyKey(context); if (key is null) return Problem("idempotency.key_required", 400);
        return ToResult(await service.LogoutAllAsync(current.UserId.Value, key, cancellationToken));
    }

    private static async Task<IResult> MeAsync(ICurrentUser current, IAuthenticationService service, CancellationToken cancellationToken) => current.UserId is null ? Problem(AuthenticationErrorCodes.SessionInvalid, 401) : ToResult(await service.GetCurrentUserAsync(current.UserId.Value, cancellationToken));
    private static async Task<IResult> SessionsAsync(ICurrentUser current, IAuthenticationService service, CancellationToken cancellationToken) => current.UserId is null || current.SessionId is null ? Problem(AuthenticationErrorCodes.SessionInvalid, 401) : Results.Ok(await service.GetSessionsAsync(current.UserId.Value, current.SessionId.Value, cancellationToken));

    private static async Task<IResult> RevokeSessionAsync(Guid sessionId, HttpContext context, ICurrentUser current, IAuthenticationService service, CancellationToken cancellationToken)
    {
        if (current.UserId is null) return Problem(AuthenticationErrorCodes.SessionInvalid, 401); string? key = IdempotencyKey(context); if (key is null) return Problem("idempotency.key_required", 400);
        return ToResult(await service.RevokeSessionAsync(current.UserId.Value, new LoginSessionId(sessionId), key, cancellationToken));
    }

    private static async Task<IResult> CreateOtpAsync(OtpChallengeRequest request, HttpContext context, IAuthenticationService service, AuthenticationRequestRateLimiter limiter, CancellationToken cancellationToken)
    {
        string? key = IdempotencyKey(context); if (key is null) return Problem("idempotency.key_required", 400); string owner = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        IResult? limited = await ApplyRateLimitAsync(limiter, AuthenticationRateLimitKind.OtpGeneration, context, request.Destination, request.DeviceIdentifier + ":" + request.Purpose, cancellationToken); if (limited is not null) return limited;
        return ToResult(await service.CreateOtpChallengeAsync(request, owner, key, Context(context), cancellationToken));
    }

    private static async Task<IResult> VerifyOtpAsync(Guid challengeId, OtpVerifyRequest request, HttpContext context, IAuthenticationService service, AuthenticationRequestRateLimiter limiter, CancellationToken cancellationToken)
    {
        IResult? limited = await ApplyRateLimitAsync(limiter, AuthenticationRateLimitKind.OtpVerification, context, challengeId.ToString("N"), request.DeviceIdentifier, cancellationToken); if (limited is not null) return limited;
        return ToResult(await service.VerifyOtpAsync(new OtpChallengeId(challengeId), request, cancellationToken));
    }
    private static AuthenticationRequestContext Context(HttpContext context) => new(context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent.ToString(), context.TraceIdentifier);
    private static string? IdempotencyKey(HttpContext context) { string value = context.Request.Headers["Idempotency-Key"].ToString(); return value.Length is >= 8 and <= 200 ? value : null; }
    private static IResult TokenResult(AuthenticationResult<TokenResponse> result, HttpResponse response) { if (!result.Succeeded) return Problem(result.ErrorCode!, result.StatusCode); response.Headers.CacheControl = "no-store"; response.Headers.Pragma = "no-cache"; return Results.Ok(result.Value); }
    private static IResult ToResult<T>(AuthenticationResult<T> result) => result.Succeeded ? Results.Ok(result.Value) : Problem(result.ErrorCode!, result.StatusCode);
    private static IResult Problem(string code, int status) => Results.Problem(statusCode: status, title: status switch { 400 => "Invalid request", 401 => "Unauthorized", 403 => "Forbidden", 409 => "Conflict", 429 => "Too many requests", 503 => "Service unavailable", _ => "Request failed" }, extensions: new Dictionary<string, object?> { ["code"] = code });
    private static string TokenSubject(string token) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token ?? string.Empty)));
    private static async Task<IResult?> ApplyRateLimitAsync(AuthenticationRequestRateLimiter limiter, AuthenticationRateLimitKind kind, HttpContext context, string subject, string deviceIdentifier, CancellationToken cancellationToken)
    {
        AuthenticationRateLimitDecision decision = await limiter.AcquireAsync(kind, context, subject, deviceIdentifier ?? string.Empty, cancellationToken); if (decision.IsAllowed) return null;
        context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling((decision.RetryAfter ?? TimeSpan.FromSeconds(60)).TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture); return Problem("auth.rate_limit_exceeded", 429);
    }
}
