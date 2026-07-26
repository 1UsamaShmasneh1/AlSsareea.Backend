using System.Net;
using System.Net.Http.Headers;

namespace AlSsareea.IntegrationTests;

public sealed class MediaStartupTests
{
    private const string UnusedConnection =
        "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1";

    [Fact]
    public async Task EndpointDiscoverySucceedsAndUploadRequiresAuthentication()
    {
        await using var factory = new ApiFactory(UnusedConnection);
        using HttpClient client = factory.CreateClient();
        using var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using HttpResponseMessage response = await client.PostAsync("/api/media/assets", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
