using System.Net.Http.Json;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class OrdersEndpointTests(PostgresFixture fixture)
{
    [Fact]
    public async Task OrdersEndpointsRequireAuthentication()
    {
        using HttpClient client = fixture.ApiFactory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/orders?page=1&pageSize=20")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/orders", new { cartId = Guid.NewGuid(), deliveryAddressId = Guid.NewGuid(), orderType = 1 })).StatusCode);
    }
}
