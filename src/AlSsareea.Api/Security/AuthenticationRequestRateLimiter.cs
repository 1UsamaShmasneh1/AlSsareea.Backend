using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using Microsoft.Extensions.Options;

namespace AlSsareea.Api.Security;

internal enum AuthenticationRateLimitKind { Login, Refresh, OtpGeneration, OtpVerification }
internal readonly record struct AuthenticationRateLimitDecision(bool IsAllowed, TimeSpan? RetryAfter);

internal sealed class AuthenticationRequestRateLimiter : IAsyncDisposable
{
    private readonly PartitionedRateLimiter<string> _login;
    private readonly PartitionedRateLimiter<string> _refresh;
    private readonly PartitionedRateLimiter<string> _otpGeneration;
    private readonly PartitionedRateLimiter<string> _otpVerification;

    public AuthenticationRequestRateLimiter(IOptions<AuthenticationRateLimitOptions> options)
    {
        AuthenticationRateLimitOptions value = options.Value;
        _login = Create(value.LoginPermitLimit, value.WindowSeconds);
        _refresh = Create(value.RefreshPermitLimit, value.WindowSeconds);
        _otpGeneration = Create(value.OtpPermitLimit, value.WindowSeconds);
        _otpVerification = Create(value.OtpPermitLimit, value.WindowSeconds);
    }

    public async ValueTask<AuthenticationRateLimitDecision> AcquireAsync(AuthenticationRateLimitKind kind, HttpContext context, string subject, string deviceIdentifier, CancellationToken cancellationToken)
    {
        string normalizedSubject = kind is AuthenticationRateLimitKind.Login or AuthenticationRateLimitKind.OtpGeneration ? NormalizeIdentifier(subject) : subject;
        string key = Hash(string.Join('\u001f', context.Connection.RemoteIpAddress?.ToString() ?? "unknown", normalizedSubject, deviceIdentifier.Trim().ToLowerInvariant(), kind.ToString()));
        PartitionedRateLimiter<string> limiter = kind switch { AuthenticationRateLimitKind.Login => _login, AuthenticationRateLimitKind.Refresh => _refresh, AuthenticationRateLimitKind.OtpGeneration => _otpGeneration, _ => _otpVerification };
        using RateLimitLease lease = await limiter.AcquireAsync(key, 1, cancellationToken);
        return new AuthenticationRateLimitDecision(lease.IsAcquired, lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter) ? retryAfter : null);
    }

    public async ValueTask DisposeAsync()
    {
        await _login.DisposeAsync(); await _refresh.DisposeAsync(); await _otpGeneration.DisposeAsync(); await _otpVerification.DisposeAsync();
    }

    private static PartitionedRateLimiter<string> Create(int permitLimit, int windowSeconds) => PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromSeconds(windowSeconds), QueueLimit = 0, AutoReplenishment = true }));
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string NormalizeIdentifier(string value)
    {
        try { return value.Contains('@', StringComparison.Ordinal) ? new Email(value).Normalized : new PhoneNumber(value).Normalized; }
        catch (DomainException) { return value.Trim().ToUpper(CultureInfo.InvariantCulture); }
    }
}
