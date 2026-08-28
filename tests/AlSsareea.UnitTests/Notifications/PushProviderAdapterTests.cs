using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Domain;
using AlSsareea.Modules.Notifications.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AlSsareea.UnitTests.Notifications;

public sealed class PushProviderAdapterTests
{
    private static readonly DateTime Now = new(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);
    private const string DeviceToken = "provider-device-token-that-must-not-be-logged";

    [Fact]
    public async Task FcmMissingConfigurationReturnsNotConfiguredWithoutNetwork()
    {
        RecordingHandler handler = new((_, _) => Response(HttpStatusCode.InternalServerError)); FcmPushAdapter adapter = Fcm(handler, new FcmProviderOptions());
        ProviderSendResult result = await adapter.SendAsync(Request(), default);
        Assert.Equal(ProviderFailureKind.NotConfigured, result.FailureKind); Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task FcmBuildsAuthenticatedHttpV1RequestAndMapsSuccess()
    {
        FcmProviderOptions options = ValidFcm(out RSAParameters publicKey); RecordingHandler handler = new((request, _) => request.RequestUri!.Host == "oauth2.googleapis.com" ? Response(HttpStatusCode.OK, "{\"access_token\":\"oauth-access\",\"expires_in\":3600}") : Response(HttpStatusCode.OK, "{\"name\":\"projects/project/messages/message-1\"}")); FcmPushAdapter adapter = Fcm(handler, options);
        ProviderSendResult result = await adapter.SendAsync(Request(), default);
        Assert.True(result.Accepted); Assert.Equal("projects/project/messages/message-1", result.ProviderMessageId); Assert.Equal(2, handler.Requests.Count);
        CapturedRequest oauth = handler.Requests[0]; Assert.Contains("grant_type=", oauth.Body, StringComparison.Ordinal); Assert.Contains("assertion=", oauth.Body, StringComparison.Ordinal); Assert.DoesNotContain("PRIVATE KEY", oauth.Body, StringComparison.Ordinal); string assertion = Form(oauth.Body)["assertion"]; string[] assertionParts = assertion.Split('.'); Assert.Equal(3, assertionParts.Length); using JsonDocument assertionHeader = JsonDocument.Parse(Base64UrlDecode(assertionParts[0])); using JsonDocument assertionPayload = JsonDocument.Parse(Base64UrlDecode(assertionParts[1])); Assert.Equal("RS256", assertionHeader.RootElement.GetProperty("alg").GetString()); Assert.Equal(options.ClientEmail, assertionPayload.RootElement.GetProperty("iss").GetString()); Assert.Equal("https://www.googleapis.com/auth/firebase.messaging", assertionPayload.RootElement.GetProperty("scope").GetString()); Assert.Equal(options.TokenUri, assertionPayload.RootElement.GetProperty("aud").GetString()); using RSA verifier = RSA.Create(); verifier.ImportParameters(publicKey); Assert.True(verifier.VerifyData(Encoding.ASCII.GetBytes(assertionParts[0] + "." + assertionParts[1]), Base64UrlDecode(assertionParts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        CapturedRequest send = handler.Requests[1]; Assert.Equal("https://fcm.googleapis.com/v1/projects/project/messages:send", send.Uri.ToString()); Assert.Equal("Bearer oauth-access", send.Authorization); using JsonDocument json = JsonDocument.Parse(send.Body); JsonElement message = json.RootElement.GetProperty("message"); Assert.Equal(DeviceToken, message.GetProperty("token").GetString()); Assert.Equal("Subject", message.GetProperty("notification").GetProperty("title").GetString()); Assert.Equal("Body", message.GetProperty("notification").GetProperty("body").GetString()); Assert.Equal(Request().DeliveryId.Value.ToString("N"), message.GetProperty("data").GetProperty("deliveryId").GetString());
    }

    [Theory]
    [InlineData(404, "UNREGISTERED", ProviderFailureKind.InvalidToken, "notifications.provider.fcm.invalid_token")]
    [InlineData(401, "UNAUTHENTICATED", ProviderFailureKind.Permanent, "notifications.provider.fcm.authentication_failed")]
    [InlineData(429, "RESOURCE_EXHAUSTED", ProviderFailureKind.RateLimited, "notifications.provider.fcm.rate_limited")]
    [InlineData(503, "UNAVAILABLE", ProviderFailureKind.Transient, "notifications.provider.fcm.transient")]
    [InlineData(400, "INVALID_ARGUMENT", ProviderFailureKind.Permanent, "notifications.provider.fcm.rejected")]
    public async Task FcmMapsProviderFailures(int statusCode, string providerStatus, ProviderFailureKind expectedKind, string expectedCode)
    {
        RecordingHandler handler = new((request, _) => request.RequestUri!.Host == "oauth2.googleapis.com" ? Response(HttpStatusCode.OK, "{\"access_token\":\"oauth-access\",\"expires_in\":3600}") : Response((HttpStatusCode)statusCode, $"{{\"error\":{{\"status\":\"{providerStatus}\"}}}}"));
        ProviderSendResult result = await Fcm(handler, ValidFcm()).SendAsync(Request(), default);
        Assert.Equal(expectedKind, result.FailureKind); Assert.Equal(expectedCode, result.ErrorCode);
    }

    [Fact]
    public async Task FcmAuthenticationFailureIsDistinctAndDoesNotSendMessage()
    {
        RecordingHandler handler = new((_, _) => Response(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_grant\"}")); ProviderSendResult result = await Fcm(handler, ValidFcm()).SendAsync(Request(), default);
        Assert.Equal(ProviderFailureKind.Permanent, result.FailureKind); Assert.Equal("notifications.provider.fcm.authentication_failed", result.ErrorCode); Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task FcmInvalidRegistrationTokenMessageMapsToInvalidToken()
    {
        RecordingHandler handler = new((request, _) => request.RequestUri!.Host == "oauth2.googleapis.com" ? Response(HttpStatusCode.OK, "{\"access_token\":\"oauth-access\",\"expires_in\":3600}") : Response(HttpStatusCode.BadRequest, "{\"error\":{\"status\":\"INVALID_ARGUMENT\",\"message\":\"The registration token is not a valid FCM registration token\"}}")); ProviderSendResult result = await Fcm(handler, ValidFcm()).SendAsync(Request(), default); Assert.Equal(ProviderFailureKind.InvalidToken, result.FailureKind);
    }

    [Fact]
    public async Task FcmHonorsCallerCancellation()
    {
        RecordingHandler handler = new(async (_, cancellationToken) => { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return Response(HttpStatusCode.OK); }); using CancellationTokenSource cancellation = new(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Fcm(handler, ValidFcm()).SendAsync(Request(), cancellation.Token));
    }

    [Fact]
    public async Task FcmStructuredDiagnosticsDoNotContainDeviceToken()
    {
        TestLogger<FcmPushAdapter> logger = new(); RecordingHandler handler = new((request, _) => request.RequestUri!.Host == "oauth2.googleapis.com" ? Response(HttpStatusCode.OK, "{\"access_token\":\"oauth-access\",\"expires_in\":3600}") : Response(HttpStatusCode.BadRequest, "{\"error\":{\"status\":\"INVALID_ARGUMENT\"}}"));
        await Fcm(handler, ValidFcm(), logger).SendAsync(Request(), default); Assert.DoesNotContain(DeviceToken, string.Join('|', logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApnsMissingConfigurationReturnsNotConfiguredWithoutNetwork()
    {
        RecordingHandler handler = new((_, _) => Response(HttpStatusCode.InternalServerError)); ProviderSendResult result = await Apns(handler, new ApnsProviderOptions()).SendAsync(Request(), default);
        Assert.Equal(ProviderFailureKind.NotConfigured, result.FailureKind); Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ApnsBuildsValidJwtHeadersPayloadAndMapsSuccess()
    {
        (string privateKey, ECParameters publicKey) = ApnsKey(); ApnsProviderOptions options = ValidApns(privateKey, true); RecordingHandler handler = new((_, _) => Response(HttpStatusCode.OK, headers: new Dictionary<string, string> { ["apns-id"] = "apns-message-1" }));
        ProviderSendResult result = await Apns(handler, options).SendAsync(Request(), default); Assert.True(result.Accepted); Assert.Equal("apns-message-1", result.ProviderMessageId);
        CapturedRequest sent = Assert.Single(handler.Requests); Assert.Equal(HttpVersion.Version20, sent.Version); Assert.Equal($"https://api.sandbox.push.apple.com/3/device/{DeviceToken}", sent.Uri.ToString()); Assert.Equal("com.alssareea.app", sent.ApnsTopic); Assert.Equal("alert", sent.ApnsPushType); Assert.StartsWith("bearer ", sent.Authorization, StringComparison.OrdinalIgnoreCase);
        string jwt = sent.Authorization[7..]; string[] parts = jwt.Split('.'); Assert.Equal(3, parts.Length); using JsonDocument header = JsonDocument.Parse(Base64UrlDecode(parts[0])); using JsonDocument payload = JsonDocument.Parse(Base64UrlDecode(parts[1])); Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString()); Assert.Equal("KEY123", header.RootElement.GetProperty("kid").GetString()); Assert.Equal("TEAM123", payload.RootElement.GetProperty("iss").GetString()); Assert.Equal(new DateTimeOffset(Now).ToUnixTimeSeconds(), payload.RootElement.GetProperty("iat").GetInt64()); using ECDsa verifier = ECDsa.Create(publicKey); Assert.True(verifier.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        using JsonDocument json = JsonDocument.Parse(sent.Body); JsonElement alert = json.RootElement.GetProperty("aps").GetProperty("alert"); Assert.Equal("Subject", alert.GetProperty("title").GetString()); Assert.Equal("Body", alert.GetProperty("body").GetString());
    }

    [Theory]
    [InlineData(400, "BadDeviceToken", ProviderFailureKind.InvalidToken, "notifications.provider.apns.invalid_token")]
    [InlineData(410, "Unregistered", ProviderFailureKind.InvalidToken, "notifications.provider.apns.invalid_token")]
    [InlineData(403, "InvalidProviderToken", ProviderFailureKind.Permanent, "notifications.provider.apns.authentication_failed")]
    [InlineData(429, "TooManyRequests", ProviderFailureKind.RateLimited, "notifications.provider.apns.rate_limited")]
    [InlineData(500, "InternalServerError", ProviderFailureKind.Transient, "notifications.provider.apns.transient")]
    [InlineData(400, "BadTopic", ProviderFailureKind.Permanent, "notifications.provider.apns.rejected")]
    public async Task ApnsMapsProviderFailures(int statusCode, string reason, ProviderFailureKind expectedKind, string expectedCode)
    {
        (string privateKey, _) = ApnsKey(); RecordingHandler handler = new((_, _) => Response((HttpStatusCode)statusCode, $"{{\"reason\":\"{reason}\"}}")); ProviderSendResult result = await Apns(handler, ValidApns(privateKey, false)).SendAsync(Request(), default);
        Assert.Equal(expectedKind, result.FailureKind); Assert.Equal(expectedCode, result.ErrorCode);
    }

    [Theory]
    [InlineData(false, "api.push.apple.com")]
    [InlineData(true, "api.sandbox.push.apple.com")]
    public async Task ApnsSelectsConfiguredEndpoint(bool sandbox, string expectedHost)
    {
        (string privateKey, _) = ApnsKey(); RecordingHandler handler = new((_, _) => Response(HttpStatusCode.OK)); await Apns(handler, ValidApns(privateKey, sandbox)).SendAsync(Request(), default); Assert.Equal(expectedHost, Assert.Single(handler.Requests).Uri.Host);
    }

    [Fact]
    public async Task ApnsReusesJwtWithinSafeLifetime()
    {
        (string privateKey, _) = ApnsKey(); RecordingHandler handler = new((_, _) => Response(HttpStatusCode.OK)); ApnsPushAdapter adapter = Apns(handler, ValidApns(privateKey, false)); await adapter.SendAsync(Request(), default); await adapter.SendAsync(Request(), default);
        Assert.Equal(2, handler.Requests.Count); Assert.Equal(handler.Requests[0].Authorization, handler.Requests[1].Authorization);
    }

    [Fact]
    public async Task ApnsHonorsCallerCancellation()
    {
        (string privateKey, _) = ApnsKey(); RecordingHandler handler = new(async (_, cancellationToken) => { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return Response(HttpStatusCode.OK); }); using CancellationTokenSource cancellation = new(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Apns(handler, ValidApns(privateKey, false)).SendAsync(Request(), cancellation.Token));
    }

    [Fact]
    public async Task ApnsStructuredDiagnosticsDoNotContainDeviceToken()
    {
        (string privateKey, _) = ApnsKey(); TestLogger<ApnsPushAdapter> logger = new(); RecordingHandler handler = new((_, _) => Response(HttpStatusCode.BadRequest, "{\"reason\":\"BadTopic\"}")); await Apns(handler, ValidApns(privateKey, false), logger).SendAsync(Request(), default);
        Assert.DoesNotContain(DeviceToken, string.Join('|', logger.Messages), StringComparison.Ordinal);
    }

    private static FcmPushAdapter Fcm(RecordingHandler handler, FcmProviderOptions options, ILogger<FcmPushAdapter>? logger = null) => new(new HttpClient(handler), Options.Create(options), new FcmCredentialCache(), new FixedClock(Now), logger ?? NullLogger<FcmPushAdapter>.Instance);
    private static ApnsPushAdapter Apns(RecordingHandler handler, ApnsProviderOptions options, ILogger<ApnsPushAdapter>? logger = null) => new(new HttpClient(handler), Options.Create(options), new ApnsJwtCache(), new FixedClock(Now), logger ?? NullLogger<ApnsPushAdapter>.Instance);
    private static ProviderSendRequest Request() => new(new NotificationDeliveryId(Guid.Parse("11111111-1111-1111-1111-111111111111")), NotificationChannel.Push, "push", DeviceToken, "Subject", "Body");
    private static FcmProviderOptions ValidFcm() => ValidFcm(out _);
    private static FcmProviderOptions ValidFcm(out RSAParameters publicKey) { using RSA rsa = RSA.Create(2048); publicKey = rsa.ExportParameters(false); return new() { Enabled = true, ProjectId = "project", ClientEmail = "service@example.iam.gserviceaccount.com", PrivateKey = rsa.ExportPkcs8PrivateKeyPem(), TimeoutSeconds = 5 }; }
    private static ApnsProviderOptions ValidApns(string privateKey, bool sandbox) => new() { Enabled = true, TeamId = "TEAM123", KeyId = "KEY123", BundleId = "com.alssareea.app", PrivateKey = privateKey, UseSandbox = sandbox, TimeoutSeconds = 5 };
    private static (string PrivateKey, ECParameters PublicKey) ApnsKey() { using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256); return (key.ExportPkcs8PrivateKeyPem(), key.ExportParameters(false)); }
    private static HttpResponseMessage Response(HttpStatusCode statusCode, string body = "{}", IReadOnlyDictionary<string, string>? headers = null) { HttpResponseMessage response = new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") }; if (headers is not null) foreach ((string key, string value) in headers) response.Headers.TryAddWithoutValidation(key, value); return response; }
    private static byte[] Base64UrlDecode(string value) { string padded = value.Replace('-', '+').Replace('_', '/'); padded += new string('=', (4 - (padded.Length % 4)) % 4); return Convert.FromBase64String(padded); }
    private static Dictionary<string, string> Form(string value) => value.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=', 2)).ToDictionary(x => Uri.UnescapeDataString(x[0].Replace('+', ' ')), x => Uri.UnescapeDataString(x[1].Replace('+', ' ')), StringComparer.Ordinal);
    private sealed class FixedClock(DateTime utcNow) : IClock { public DateTime UtcNow { get; } = utcNow; }

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : this((request, cancellationToken) => Task.FromResult(responder(request, cancellationToken))) { }
        public List<CapturedRequest> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken); Requests.Add(new(request.Method, request.RequestUri!, request.Version, request.Headers.Authorization?.ToString() ?? string.Empty, Header(request, "apns-topic"), Header(request, "apns-push-type"), body)); return await responder(request, cancellationToken);
        }
        private static string Header(HttpRequestMessage request, string name) => request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.Single() : string.Empty;
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, Version Version, string Authorization, string ApnsTopic, string ApnsPushType, string Body);
    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
