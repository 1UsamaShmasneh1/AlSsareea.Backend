using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlSsareea.Api.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AlSsareea.IntegrationTests;

public sealed class SystemEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
    });

    [Fact]
    public async Task HealthReturnsSuccess()
    {
        using HttpResponseMessage response = await _client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemInfoReturnsExpectedPublicInformation()
    {
        using HttpResponseMessage response = await _client.GetAsync(
            "/api/system/info",
            CancellationToken.None);
        using JsonDocument document = await response.Content.ReadFromJsonAsync<JsonDocument>(
            CancellationToken.None) ?? throw new InvalidOperationException("System info response was empty.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("AlSsareea.Backend", document.RootElement.GetProperty("service").GetString());
        Assert.Equal("Testing", document.RootElement.GetProperty("environment").GetString());
        Assert.Equal("1.0", document.RootElement.GetProperty("apiVersion").GetString());
        Assert.Equal(3, document.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task RequestReturnsCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "integration-test-123");

        using HttpResponseMessage response = await _client.SendAsync(
            request,
            CancellationToken.None);

        Assert.Equal(
            "integration-test-123",
            Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
    }

    [Fact]
    public async Task RequestWithSupportedLanguageUsesThatLanguage()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/system/info");
        request.Headers.AcceptLanguage.ParseAdd("he");

        using HttpResponseMessage response = await _client.SendAsync(
            request,
            CancellationToken.None);

        Assert.Equal("he", Assert.Single(response.Content.Headers.ContentLanguage));
    }

    [Fact]
    public async Task SystemInfoDoesNotExposeSensitiveInformation()
    {
        string response = await _client.GetStringAsync(
            "/api/system/info",
            CancellationToken.None);

        Assert.DoesNotContain("connectionString", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("machineName", response, StringComparison.OrdinalIgnoreCase);
    }
}
