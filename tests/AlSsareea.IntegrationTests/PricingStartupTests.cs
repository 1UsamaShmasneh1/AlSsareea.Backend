using System.Net;
using System.Net.Http.Json;

namespace AlSsareea.IntegrationTests;

public sealed class PricingStartupTests
{
    private const string UnusedConnection =
        "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1";

    [Fact]
    public async Task EndpointDiscoverySucceedsAndEstimateRequiresAuthentication()
    {
        await using var factory = new ApiFactory(UnusedConnection);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/pricing/estimates",
            new { merchantId = Guid.NewGuid(), currency = "ILS", itemsSubtotalMinor = 1000 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
