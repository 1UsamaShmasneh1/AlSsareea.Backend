using System.Net;
using System.Net.Http.Json;

namespace AlSsareea.IntegrationTests;

public sealed class CatalogStartupTests
{
    private const string UnusedConnection =
        "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1";

    [Fact]
    public async Task EndpointDiscoverySucceedsAndManagementRequiresAuthentication()
    {
        await using var factory = new ApiFactory(UnusedConnection);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/merchants/{Guid.NewGuid()}/catalog",
            new { name = "Catalog", defaultLanguage = "en" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
