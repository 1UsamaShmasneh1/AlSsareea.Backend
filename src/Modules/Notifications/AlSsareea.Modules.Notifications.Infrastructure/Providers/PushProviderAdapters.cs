using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Notifications.Infrastructure.Providers;

public sealed class FcmProviderOptions
{
    public const string SectionName = "Notifications:Providers:Fcm";
    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string TokenUri { get; set; } = "https://oauth2.googleapis.com/token";
    public int TimeoutSeconds { get; set; } = 30;
    internal bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ProjectId) && !string.IsNullOrWhiteSpace(ClientEmail) && !string.IsNullOrWhiteSpace(PrivateKey);
    internal bool IsValid() => !Enabled || (IsConfigured && Uri.TryCreate(TokenUri, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps && TimeoutSeconds is >= 1 and <= 120);
}

public sealed class ApnsProviderOptions
{
    public const string SectionName = "Notifications:Providers:Apns";
    public bool Enabled { get; set; }
    public string TeamId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string BundleId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public bool UseSandbox { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    internal bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(TeamId) && !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(BundleId) && !string.IsNullOrWhiteSpace(PrivateKey);
    internal bool IsValid() => !Enabled || (IsConfigured && TimeoutSeconds is >= 1 and <= 120);
}

internal abstract class ProviderCredentialCache
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal string? Value { get; set; }
    internal DateTime ExpiresAtUtc { get; set; }
    internal bool TryGet(DateTime now, out string value)
    {
        value = Value ?? string.Empty;
        return value.Length != 0 && ExpiresAtUtc > now.AddMinutes(1);
    }
}

internal sealed class FcmCredentialCache : ProviderCredentialCache;
internal sealed class ApnsJwtCache : ProviderCredentialCache;

internal sealed class FcmPushAdapter(HttpClient httpClient, IOptions<FcmProviderOptions> options, FcmCredentialCache cache, IClock clock, ILogger<FcmPushAdapter> logger) : IFcmPushAdapter
{
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Action<ILogger, Guid, int, string, Exception?> FailureLog = LoggerMessage.Define<Guid, int, string>(LogLevel.Warning, new EventId(1710, "FcmSendFailed"), "FCM delivery {DeliveryId} failed with HTTP {StatusCode} and classification {ErrorCode}.");
    private readonly FcmProviderOptions _options = options.Value;

    public async Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured) return Failure(ProviderFailureKind.NotConfigured, "notifications.provider.fcm.not_configured");
        if (string.IsNullOrWhiteSpace(request.Token)) return Failure(ProviderFailureKind.InvalidToken, "notifications.provider.fcm.invalid_token");
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            AccessTokenResult access = await GetAccessTokenAsync(timeout.Token); if (!access.Success) return access.Failure!;
            Uri endpoint = new($"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/messages:send");
            var payload = new { message = new { token = request.Token, notification = new { title = request.Subject, body = request.Body }, data = new Dictionary<string, string> { ["deliveryId"] = request.DeliveryId.Value.ToString("N") } } };
            using HttpRequestMessage message = new(HttpMethod.Post, endpoint) { Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json") }; message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.Token);
            using HttpResponseMessage response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token); string body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (response.IsSuccessStatusCode) return new(true, false, ProviderFailureKind.None, ProviderMessageId: FcmMessageName(body));
            ProviderSendResult failure = MapFailure(response.StatusCode, body); FailureLog(logger, request.DeliveryId.Value, (int)response.StatusCode, failure.ErrorCode!, null); return failure;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Failure(ProviderFailureKind.Transient, "notifications.provider.fcm.timeout"); }
        catch (HttpRequestException) { FailureLog(logger, request.DeliveryId.Value, 0, "notifications.provider.fcm.transport", null); return Failure(ProviderFailureKind.Transient, "notifications.provider.fcm.transport"); }
        catch (CryptographicException) { FailureLog(logger, request.DeliveryId.Value, 0, "notifications.provider.fcm.credentials_invalid", null); return Failure(ProviderFailureKind.Permanent, "notifications.provider.fcm.credentials_invalid"); }
        catch (JsonException) { FailureLog(logger, request.DeliveryId.Value, 0, "notifications.provider.fcm.response_invalid", null); return Failure(ProviderFailureKind.Transient, "notifications.provider.fcm.response_invalid"); }
    }

    private async Task<AccessTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        DateTime now = clock.UtcNow; if (cache.TryGet(now, out string cached)) return AccessTokenResult.Ok(cached);
        await cache.Gate.WaitAsync(cancellationToken);
        try
        {
            now = clock.UtcNow; if (cache.TryGet(now, out cached)) return AccessTokenResult.Ok(cached);
            string assertion = CreateGoogleAssertion(now);
            using HttpRequestMessage request = new(HttpMethod.Post, _options.TokenUri) { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer", ["assertion"] = assertion }) };
            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ProviderFailureKind kind = response.StatusCode == HttpStatusCode.TooManyRequests ? ProviderFailureKind.RateLimited : (int)response.StatusCode >= 500 ? ProviderFailureKind.Transient : ProviderFailureKind.Permanent;
                return AccessTokenResult.Fail(Failure(kind, "notifications.provider.fcm.authentication_failed"));
            }
            using JsonDocument document = JsonDocument.Parse(body); if (!document.RootElement.TryGetProperty("access_token", out JsonElement accessToken) || string.IsNullOrWhiteSpace(accessToken.GetString())) throw new JsonException("FCM access_token is missing."); string token = accessToken.GetString()!; int expiresIn = document.RootElement.TryGetProperty("expires_in", out JsonElement expiry) && expiry.TryGetInt32(out int seconds) ? seconds : 3600;
            cache.Value = token; cache.ExpiresAtUtc = now.AddSeconds(Math.Max(1, expiresIn - 60)); return AccessTokenResult.Ok(token);
        }
        finally { cache.Gate.Release(); }
    }

    private string CreateGoogleAssertion(DateTime now)
    {
        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }, JsonOptions)); long issued = new DateTimeOffset(now).ToUnixTimeSeconds();
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = _options.ClientEmail, scope = MessagingScope, aud = _options.TokenUri, iat = issued, exp = issued + 3600 }, JsonOptions)); string signingInput = header + "." + payload;
        using RSA rsa = RSA.Create(); rsa.ImportFromPem(_options.PrivateKey); byte[] signature = rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); return signingInput + "." + Base64Url(signature);
    }

    private static ProviderSendResult MapFailure(HttpStatusCode statusCode, string responseBody)
    {
        string status = FcmStatus(responseBody);
        if (IsFcmInvalidToken(responseBody)) return Failure(ProviderFailureKind.InvalidToken, "notifications.provider.fcm.invalid_token");
        if (statusCode == HttpStatusCode.TooManyRequests || string.Equals(status, "RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)) return Failure(ProviderFailureKind.RateLimited, "notifications.provider.fcm.rate_limited");
        if ((int)statusCode >= 500 || string.Equals(status, "UNAVAILABLE", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "INTERNAL", StringComparison.OrdinalIgnoreCase)) return Failure(ProviderFailureKind.Transient, "notifications.provider.fcm.transient");
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || string.Equals(status, "UNAUTHENTICATED", StringComparison.OrdinalIgnoreCase)) return Failure(ProviderFailureKind.Permanent, "notifications.provider.fcm.authentication_failed");
        return Failure(ProviderFailureKind.Permanent, "notifications.provider.fcm.rejected");
    }

    private static string FcmStatus(string body)
    {
        if (body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)) return "UNREGISTERED";
        try { using JsonDocument document = JsonDocument.Parse(body); return document.RootElement.TryGetProperty("error", out JsonElement error) && error.TryGetProperty("status", out JsonElement status) ? status.GetString() ?? string.Empty : string.Empty; }
        catch (JsonException) { return string.Empty; }
    }

    private static bool IsFcmInvalidToken(string body)
    {
        if (body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body); if (!document.RootElement.TryGetProperty("error", out JsonElement error) || !error.TryGetProperty("message", out JsonElement message)) return false; string text = message.GetString() ?? string.Empty; return text.Contains("registration token", StringComparison.OrdinalIgnoreCase) && (text.Contains("not a valid", StringComparison.OrdinalIgnoreCase) || text.Contains("invalid", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException) { return false; }
    }

    private static string? FcmMessageName(string body) { using JsonDocument document = JsonDocument.Parse(body); return document.RootElement.TryGetProperty("name", out JsonElement name) ? name.GetString() : null; }
    private static ProviderSendResult Failure(ProviderFailureKind kind, string code) => new(false, false, kind, code);
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private readonly record struct AccessTokenResult(bool Success, string? Token, ProviderSendResult? Failure) { public static AccessTokenResult Ok(string token) => new(true, token, null); public static AccessTokenResult Fail(ProviderSendResult failure) => new(false, null, failure); }
}

internal sealed class ApnsPushAdapter(HttpClient httpClient, IOptions<ApnsProviderOptions> options, ApnsJwtCache cache, IClock clock, ILogger<ApnsPushAdapter> logger) : IApnsPushAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Action<ILogger, Guid, int, string, Exception?> FailureLog = LoggerMessage.Define<Guid, int, string>(LogLevel.Warning, new EventId(1711, "ApnsSendFailed"), "APNs delivery {DeliveryId} failed with HTTP {StatusCode} and classification {ErrorCode}.");
    private readonly ApnsProviderOptions _options = options.Value;

    public async Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured) return Failure(ProviderFailureKind.NotConfigured, "notifications.provider.apns.not_configured");
        if (string.IsNullOrWhiteSpace(request.Token)) return Failure(ProviderFailureKind.InvalidToken, "notifications.provider.apns.invalid_token");
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            string jwt = await GetJwtAsync(timeout.Token); string host = _options.UseSandbox ? "https://api.sandbox.push.apple.com/3" : "https://api.push.apple.com/3"; Uri endpoint = new($"{host}/device/{Uri.EscapeDataString(request.Token)}");
            var payload = new { aps = new { alert = new { title = request.Subject, body = request.Body } }, deliveryId = request.DeliveryId.Value.ToString("N") }; byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions); if (json.Length > 4096) return Failure(ProviderFailureKind.Permanent, "notifications.provider.apns.payload_too_large");
            using HttpRequestMessage message = new(HttpMethod.Post, endpoint) { Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact, Content = new ByteArrayContent(json) }; message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json"); message.Headers.Authorization = new AuthenticationHeaderValue("bearer", jwt); message.Headers.TryAddWithoutValidation("apns-topic", _options.BundleId); message.Headers.TryAddWithoutValidation("apns-push-type", "alert"); message.Headers.TryAddWithoutValidation("apns-priority", "10");
            using HttpResponseMessage response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token); string body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (response.IsSuccessStatusCode) return new(true, false, ProviderFailureKind.None, ProviderMessageId: Header(response, "apns-id"));
            ProviderSendResult failure = MapFailure(response.StatusCode, ApnsReason(body)); FailureLog(logger, request.DeliveryId.Value, (int)response.StatusCode, failure.ErrorCode!, null); return failure;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Failure(ProviderFailureKind.Transient, "notifications.provider.apns.timeout"); }
        catch (HttpRequestException) { FailureLog(logger, request.DeliveryId.Value, 0, "notifications.provider.apns.transport", null); return Failure(ProviderFailureKind.Transient, "notifications.provider.apns.transport"); }
        catch (CryptographicException) { FailureLog(logger, request.DeliveryId.Value, 0, "notifications.provider.apns.credentials_invalid", null); return Failure(ProviderFailureKind.Permanent, "notifications.provider.apns.credentials_invalid"); }
        catch (JsonException) { FailureLog(logger, request.DeliveryId.Value, 0, "notifications.provider.apns.response_invalid", null); return Failure(ProviderFailureKind.Transient, "notifications.provider.apns.response_invalid"); }
    }

    private async Task<string> GetJwtAsync(CancellationToken cancellationToken)
    {
        DateTime now = clock.UtcNow; if (cache.TryGet(now, out string cached)) return cached;
        await cache.Gate.WaitAsync(cancellationToken);
        try
        {
            now = clock.UtcNow; if (cache.TryGet(now, out cached)) return cached;
            string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", kid = _options.KeyId }, JsonOptions)); string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = _options.TeamId, iat = new DateTimeOffset(now).ToUnixTimeSeconds() }, JsonOptions)); string signingInput = header + "." + payload;
            using ECDsa key = ECDsa.Create(); key.ImportFromPem(_options.PrivateKey); byte[] signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation); cached = signingInput + "." + Base64Url(signature); cache.Value = cached; cache.ExpiresAtUtc = now.AddMinutes(50); return cached;
        }
        finally { cache.Gate.Release(); }
    }

    private static ProviderSendResult MapFailure(HttpStatusCode statusCode, string reason)
    {
        if (statusCode == HttpStatusCode.Gone || reason is "BadDeviceToken" or "DeviceTokenNotForTopic" or "Unregistered") return Failure(ProviderFailureKind.InvalidToken, "notifications.provider.apns.invalid_token");
        if (statusCode == HttpStatusCode.TooManyRequests || reason == "TooManyRequests") return Failure(ProviderFailureKind.RateLimited, "notifications.provider.apns.rate_limited");
        if ((int)statusCode >= 500) return Failure(ProviderFailureKind.Transient, "notifications.provider.apns.transient");
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || reason is "ExpiredProviderToken" or "InvalidProviderToken" or "MissingProviderToken") return Failure(ProviderFailureKind.Permanent, "notifications.provider.apns.authentication_failed");
        return Failure(ProviderFailureKind.Permanent, "notifications.provider.apns.rejected");
    }

    private static string ApnsReason(string body) { try { using JsonDocument document = JsonDocument.Parse(body); return document.RootElement.TryGetProperty("reason", out JsonElement reason) ? reason.GetString() ?? string.Empty : string.Empty; } catch (JsonException) { return string.Empty; } }
    private static string? Header(HttpResponseMessage response, string name) => response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
    private static ProviderSendResult Failure(ProviderFailureKind kind, string code) => new(false, false, kind, code);
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
