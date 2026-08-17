using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSsareea.Modules.Drivers.Domain;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using AlSsareea.Modules.Tracking.Contracts;
using AlSsareea.Modules.Tracking.Domain;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class TrackingEndpointTests(PostgresFixture fixture)
{
    private const string Password = "Valid-Password-123!";

    [Fact]
    public async Task TrackingEndpointsAndHubRequireAuthentication()
    {
        using HttpClient client = fixture.ApiFactory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/tracking/location", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/tracking/drivers/{Guid.NewGuid()}/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/tracking/orders/{Guid.NewGuid()}/latest")).StatusCode);
    }

    [Fact]
    public void RoutesDoNotExposeCustomerTrackingByArbitraryDriverId()
    {
        EndpointDataSource source = fixture.ApiFactory.Services.GetRequiredService<EndpointDataSource>(); string[] routes = source.Endpoints.OfType<RouteEndpoint>().Select(x => x.RoutePattern.RawText ?? string.Empty).ToArray();
        Assert.Contains("/api/v1/tracking/location", routes); Assert.Contains("/api/v1/tracking/locations/batch", routes); Assert.Contains("/api/v1/tracking/me/latest", routes); Assert.Contains("/api/v1/tracking/orders/{orderId:guid}/latest", routes); Assert.Contains("/hubs/tracking", routes);
        Assert.DoesNotContain(routes, route => route.Contains("customer", StringComparison.OrdinalIgnoreCase) && route.Contains("driverId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DriverSelfUpdateDuplicateStaleAndBatchFlowsAreSafe()
    {
        (Guid userId, HttpClient client) = await AuthenticatedClient(UserType.Driver, TrackingPermissions.UpdateSelf); await PersistActiveDriver(userId);
        DateTime now = DateTime.UtcNow; LocationUpdateRequest first = new(now, 31.7683, 35.2137, 5, null, null, null, 10);
        HttpResponseMessage accepted = await client.PostAsJsonAsync("/api/v1/tracking/location", first); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode); LocationUpdateResponse acceptedBody = (await accepted.Content.ReadFromJsonAsync<LocationUpdateResponse>())!; Assert.True(acceptedBody.LatestUpdated);
        HttpResponseMessage duplicate = await client.PostAsJsonAsync("/api/v1/tracking/location", first); Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode); LocationUpdateResponse duplicateBody = (await duplicate.Content.ReadFromJsonAsync<LocationUpdateResponse>())!; Assert.Equal("duplicate", duplicateBody.Status); Assert.Equal(acceptedBody.LocationId, duplicateBody.LocationId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/tracking/location", first with { RecordedAtUtc = now.AddMinutes(2), SequenceNumber = 12 })).StatusCode);
        HttpResponseMessage stale = await client.PostAsJsonAsync("/api/v1/tracking/location", first with { RecordedAtUtc = now.AddMinutes(-6), SequenceNumber = 9 }); Assert.Equal("history-only", (await stale.Content.ReadFromJsonAsync<LocationUpdateResponse>())!.Status);
        var batch = new LocationBatchRequest(Guid.NewGuid(), [first with { RecordedAtUtc = now.AddMinutes(-2), SequenceNumber = 8 }, first with { RecordedAtUtc = now.AddSeconds(1), SequenceNumber = 11 }]); HttpResponseMessage batchResponse = await client.PostAsJsonAsync("/api/v1/tracking/locations/batch", batch); Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode); Assert.Equal(2, (await batchResponse.Content.ReadFromJsonAsync<LocationBatchResponse>())!.Results.Count);
        LocationBatchResponse retriedBatch = (await (await client.PostAsJsonAsync("/api/v1/tracking/locations/batch", batch)).Content.ReadFromJsonAsync<LocationBatchResponse>())!; Assert.Equal(2, retriedBatch.Duplicates); Assert.Equal(0, retriedBatch.Accepted + retriedBatch.HistoryOnly);
        Assert.Equal(11, (await client.GetFromJsonAsync<DriverLocationResponse>("/api/v1/tracking/me/latest"))!.SequenceNumber);
    }

    [Fact]
    public async Task OperationsReadIsPermissionProtectedAndCustomerOrderVisibilityDeniesByDefault()
    {
        Guid driverId = Guid.NewGuid(); DateTime now = DateTime.UtcNow; await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope()) { var store = scope.ServiceProvider.GetRequiredService<AlSsareea.Modules.Tracking.Application.ITrackingStore>(); await store.StoreAsync(DriverLocation.Create(DriverLocationId.New(), driverId, LocationPosition.Create(31.7, 35.2), now, now, 5, null, null, null, 1, LocationSource.Live), true, default); }
        (_, HttpClient operations) = await AuthenticatedClient(UserType.Operations, TrackingPermissions.Read, TrackingPermissions.ReadHistory); Assert.Equal(HttpStatusCode.OK, (await operations.GetAsync($"/api/v1/tracking/drivers/{driverId}/latest")).StatusCode);
        (_, HttpClient unprivileged) = await AuthenticatedClient(UserType.Customer); Assert.Equal(HttpStatusCode.Forbidden, (await unprivileged.GetAsync($"/api/v1/tracking/drivers/{driverId}/latest")).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await unprivileged.GetAsync($"/api/v1/tracking/orders/{Guid.NewGuid()}/latest")).StatusCode);
    }

    private async Task<(Guid UserId, HttpClient Client)> AuthenticatedClient(UserType type, params string[] permissions)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>(); DateTime now = DateTime.UtcNow; string suffix = Guid.NewGuid().ToString("N"); string email = $"tracking-{suffix}@example.com"; User user = User.Create(UserId.New(), type, new Email(email), null, new PasswordHash(hasher.Hash(Password).EncodedHash), now); user.Activate(now); Role role = Role.Create(RoleId.New(), "tracking-role-" + suffix, null, false, now); user.AssignRole(role.Id, now);
        foreach (string name in permissions) { Permission? permission = await db.Permissions.SingleOrDefaultAsync(x => x.Name == name); if (permission is null) { permission = Permission.Create(PermissionId.New(), name, name, null, "tracking", false, now); db.Add(permission); } role.AssignPermission(permission.Id, now); }
        db.AddRange(user, role); await db.SaveChangesAsync(); HttpClient client = fixture.ApiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }); LoginRequest login = new(email, Password, new LoginDeviceRequest("tracking-" + suffix, "Tracking test", DevicePlatform.Android, "1.0", "15")); TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", login)).Content.ReadFromJsonAsync<TokenResponse>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken); return (user.Id.Value, client);
    }

    private async Task PersistActiveDriver(Guid userId)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); DateTime now = DateTime.UtcNow; Driver driver = Driver.Create(DriverId.New(), userId, "Tracking Driver", EmploymentType.Employee, 2, null, now); driver.SubmitForReview(now); driver.Approve(now); driver.Activate(now); db.Drivers.Add(driver); await db.SaveChangesAsync();
    }
}
