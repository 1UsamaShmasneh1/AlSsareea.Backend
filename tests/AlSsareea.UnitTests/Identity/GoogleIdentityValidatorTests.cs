using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace AlSsareea.UnitTests.Identity;

public sealed class GoogleIdentityValidatorTests
{
    private static readonly DateTime Now = new(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ValidVerifiedGoogleClaimsAreAccepted()
    {
        AuthenticationResult<GoogleIdentity> result = await Validate(Claims());
        Assert.True(result.Succeeded); Assert.Equal("google-subject", result.Value!.Subject); Assert.Equal("customer@example.test", result.Value.Email);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("subject")]
    [InlineData("email")]
    [InlineData("unverified")]
    [InlineData("nonce")]
    public async Task InvalidSecurityClaimIsRejected(string defect)
    {
        GoogleTokenClaims claims = Claims(); string? nonce = "expected-nonce";
        claims = defect switch
        {
            "issuer" => claims with { Issuer = "https://attacker.example" },
            "audience" => claims with { Audience = "other-client" },
            "expired" => claims with { ExpirationUtc = Now.AddSeconds(-1) },
            "subject" => claims with { Subject = "" },
            "email" => claims with { Email = "" },
            "unverified" => claims with { EmailVerified = false },
            "nonce" => claims with { Nonce = "wrong-nonce" },
            _ => claims,
        };
        AuthenticationResult<GoogleIdentity> result = await Validate(claims, nonce);
        Assert.False(result.Succeeded); Assert.Equal(AuthenticationErrorCodes.ExternalTokenInvalid, result.ErrorCode); Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task InvalidSignatureResultIsRejected()
    {
        AuthenticationResult<GoogleIdentity> result = await Validator(new(false, true, null)).ValidateAsync("invalid-token", null, default);
        Assert.Equal(AuthenticationErrorCodes.ExternalTokenInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task ProviderFailureIsReportedAsUnavailable()
    {
        AuthenticationResult<GoogleIdentity> result = await Validator(new(false, false, null)).ValidateAsync("token", null, default);
        Assert.Equal(AuthenticationErrorCodes.ExternalProviderUnavailable, result.ErrorCode); Assert.Equal(503, result.StatusCode);
    }

    private static Task<AuthenticationResult<GoogleIdentity>> Validate(GoogleTokenClaims claims, string? nonce = "expected-nonce") => Validator(new(true, true, claims)).ValidateAsync("token", nonce, default);
    private static GoogleIdentityValidator Validator(GoogleTokenVerification verification) => new(new Verifier(verification), Options.Create(new GoogleAuthenticationOptions { Enabled = true, AllowedClientIds = ["customer-client"] }), new FixedClock());
    private static GoogleTokenClaims Claims() => new("google-subject", "customer@example.test", true, "https://accounts.google.com", "customer-client", Now.AddMinutes(5), "First", "Last", "expected-nonce");
    private sealed class Verifier(GoogleTokenVerification result) : IGoogleTokenVerifier { public Task<GoogleTokenVerification> VerifyAsync(string idToken, CancellationToken cancellationToken) => Task.FromResult(result); }
    private sealed class FixedClock : IClock { public DateTime UtcNow => Now; }
}
