using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Identity.Application;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Identity.Infrastructure.Authentication;

internal sealed class GoogleTokenVerifier(IOptions<GoogleAuthenticationOptions> options) : IGoogleTokenVerifier
{
    private readonly GoogleAuthenticationOptions _options = options.Value;

    public async Task<GoogleTokenVerification> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            GoogleJsonWebSignature.Payload payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings { Audience = _options.AllowedClientIds });
            cancellationToken.ThrowIfCancellationRequested();
            return new(true, true, new(payload.Subject, payload.Email, payload.EmailVerified, payload.Issuer, payload.Audience?.ToString() ?? string.Empty, DateTimeOffset.FromUnixTimeSeconds(payload.ExpirationTimeSeconds!.Value).UtcDateTime, payload.GivenName, payload.FamilyName, payload.Nonce));
        }
        catch (InvalidJwtException) { return new(false, true, null); }
        catch (HttpRequestException) { return new(false, false, null); }
    }
}

internal sealed class GoogleIdentityValidator(IGoogleTokenVerifier verifier, IOptions<GoogleAuthenticationOptions> options, IClock clock) : IGoogleIdentityValidator
{
    private readonly GoogleAuthenticationOptions _options = options.Value;

    public async Task<AuthenticationResult<GoogleIdentity>> ValidateAsync(string idToken, string? nonce, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return AuthenticationResult<GoogleIdentity>.Failure(AuthenticationErrorCodes.ExternalProviderUnavailable, 503);
        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 16_384)
            return AuthenticationResult<GoogleIdentity>.Failure(AuthenticationErrorCodes.ExternalTokenInvalid, 401);
        GoogleTokenVerification verification = await verifier.VerifyAsync(idToken, cancellationToken);
        if (!verification.ProviderAvailable)
            return AuthenticationResult<GoogleIdentity>.Failure(AuthenticationErrorCodes.ExternalProviderUnavailable, 503);
        GoogleTokenClaims? claims = verification.Claims;
        if (!verification.Succeeded || claims is null || !claims.EmailVerified || string.IsNullOrWhiteSpace(claims.Subject) || string.IsNullOrWhiteSpace(claims.Email) ||
            claims.Issuer is not ("accounts.google.com" or "https://accounts.google.com") || !_options.AllowedClientIds.Contains(claims.Audience, StringComparer.Ordinal) || claims.ExpirationUtc <= clock.UtcNow ||
            !string.IsNullOrEmpty(nonce) && !string.Equals(claims.Nonce, nonce, StringComparison.Ordinal))
            return AuthenticationResult<GoogleIdentity>.Failure(AuthenticationErrorCodes.ExternalTokenInvalid, 401);
        return AuthenticationResult<GoogleIdentity>.Success(new(claims.Subject, claims.Email, claims.GivenName, claims.FamilyName));
    }
}
