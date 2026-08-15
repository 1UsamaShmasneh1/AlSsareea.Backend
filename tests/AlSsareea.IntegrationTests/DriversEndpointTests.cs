using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Drivers.Domain;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class DriversEndpointTests(PostgresFixture fixture)
{
    private const string Password = "Valid-Password-123!";

    [Fact]
    public async Task DriverSelfAndAdministrativeEndpointsRequireAuthentication()
    {
        using HttpClient client = fixture.ApiFactory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/drivers/me")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/drivers?page=1&pageSize=20")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/drivers/me", new { displayName = "Driver", employmentType = 1, maximumConcurrentDeliveries = 2 })).StatusCode);
    }

    [Fact]
    public void DriverLifecycleRoutesAreRegistered()
    {
        using HttpClient client = fixture.ApiFactory.CreateClient(); EndpointDataSource source = fixture.ApiFactory.Services.GetRequiredService<EndpointDataSource>(); string[] routes = source.Endpoints.OfType<RouteEndpoint>().Select(x => x.RoutePattern.RawText ?? string.Empty).ToArray(); Assert.Contains("/api/v1/drivers/me", routes); Assert.Contains("/api/v1/drivers/{driverId:guid}/suspensions", routes); Assert.Contains("/api/v1/drivers/me/availability/online", routes); Assert.Contains("/api/v1/drivers/me/shifts", routes); Assert.Contains("/api/v1/drivers/me/shifts/{shiftId:guid}/start", routes); Assert.Contains("/api/v1/drivers/{driverId:guid}/shifts", routes);
    }

    [Fact]
    public async Task IdempotencyScopesByActorAndOperationReplaysOriginalContractAndRejectsPayloadMismatch()
    {
        (Guid firstUserId, HttpClient first) = await AuthenticatedClient(UserType.Driver, DriverPermissions.ProfileManageSelf);
        var request = new CreateDriverRequest("Original Driver", (short)EmploymentType.IndependentContractor, 2, null);
        Task<HttpResponseMessage>[] concurrent = [SendWithKey(first, HttpMethod.Post, "/api/v1/drivers/me", request, "shared-create-key"), SendWithKey(first, HttpMethod.Post, "/api/v1/drivers/me", request, "shared-create-key")];
        HttpResponseMessage[] responses = await Task.WhenAll(concurrent); Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode)); DriverProfileResponse original = (await responses[0].Content.ReadFromJsonAsync<DriverProfileResponse>())!; Assert.All(responses, response => response.Dispose());

        HttpResponseMessage mismatch = await SendWithKey(first, HttpMethod.Post, "/api/v1/drivers/me", request with { DisplayName = "Different" }, "shared-create-key"); Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        HttpResponseMessage update = await SendWithKey(first, HttpMethod.Put, "/api/v1/drivers/me", new UpdateDriverProfileRequest("Updated Driver", null, null, original.ConcurrencyStamp), "shared-create-key"); Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        HttpResponseMessage replay = await SendWithKey(first, HttpMethod.Post, "/api/v1/drivers/me", request, "shared-create-key"); Assert.Equal(HttpStatusCode.Created, replay.StatusCode); Assert.Equal("Original Driver", (await replay.Content.ReadFromJsonAsync<DriverProfileResponse>())!.DisplayName);

        (Guid secondUserId, HttpClient second) = await AuthenticatedClient(UserType.Driver, DriverPermissions.ProfileManageSelf); HttpResponseMessage secondCreate = await SendWithKey(second, HttpMethod.Post, "/api/v1/drivers/me", request with { DisplayName = "Second Driver" }, "shared-create-key"); Assert.Equal(HttpStatusCode.Created, secondCreate.StatusCode); Assert.NotEqual(firstUserId, secondUserId);

        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); Assert.Equal(2, await db.IdempotencyRecords.CountAsync(x => x.Operation == "create" && (x.ActorUserId == firstUserId || x.ActorUserId == secondUserId))); Assert.Equal(2, await db.AuditRecords.CountAsync(x => x.Action == "DriverCreated" && (x.ActorUserId == firstUserId || x.ActorUserId == secondUserId)));
    }

    [Fact]
    public async Task ShiftPermissionsSeparateAdministrationFromOwnedSelfServiceAndConcealOtherDrivers()
    {
        (Guid ownerUserId, HttpClient owner) = await AuthenticatedClient(UserType.Driver, DriverPermissions.ShiftsReadSelf, DriverPermissions.ShiftsManageSelf);
        (Guid otherUserId, HttpClient other) = await AuthenticatedClient(UserType.Driver, DriverPermissions.ShiftsReadSelf, DriverPermissions.ShiftsManageSelf);
        Driver ownerDriver = await PersistDriver(ownerUserId, "Owner Driver"); Driver otherDriver = await PersistDriver(otherUserId, "Other Driver");
        (_, HttpClient admin) = await AuthenticatedClient(UserType.Operations, DriverPermissions.ShiftsManage, DriverPermissions.ShiftsRead);

        DateTime start = DateTime.UtcNow.AddHours(1); var forged = new { scheduledStartUtc = start, scheduledEndUtc = start.AddHours(2), driverId = otherDriver.Id.Value, userId = otherUserId };
        HttpResponseMessage createdResponse = await SendWithKey(admin, HttpMethod.Post, $"/api/v1/drivers/{ownerDriver.Id.Value}/shifts", forged, "admin-create-shift"); Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode); DriverShiftResponse created = (await createdResponse.Content.ReadFromJsonAsync<DriverShiftResponse>())!;
        Assert.Equal(HttpStatusCode.Forbidden, (await SendWithKey(owner, HttpMethod.Post, $"/api/v1/drivers/{ownerDriver.Id.Value}/shifts", new CreateDriverShiftRequest(start.AddHours(3), start.AddHours(4)), "forbidden-create")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/v1/drivers/me/shifts")).StatusCode); Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/drivers/me/shifts/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/drivers/me/shifts/{created.Id}")).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await SendWithKey(other, HttpMethod.Post, $"/api/v1/drivers/me/shifts/{created.Id}/start", null, "other-start")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SendWithKey(owner, HttpMethod.Post, $"/api/v1/drivers/me/shifts/{created.Id}/start", null, "owner-start")).StatusCode); Assert.Equal((short)DriverShiftStatus.Started, (await owner.GetFromJsonAsync<DriverShiftResponse>($"/api/v1/drivers/me/shifts/{created.Id}"))!.Status);
        Assert.Equal(HttpStatusCode.OK, (await SendWithKey(owner, HttpMethod.Post, $"/api/v1/drivers/me/shifts/{created.Id}/complete", null, "owner-complete")).StatusCode); Assert.Equal((short)DriverShiftStatus.Completed, (await owner.GetFromJsonAsync<DriverShiftResponse>($"/api/v1/drivers/me/shifts/{created.Id}"))!.Status);

        HttpResponseMessage secondCreatedResponse = await SendWithKey(admin, HttpMethod.Post, $"/api/v1/drivers/{ownerDriver.Id.Value}/shifts", new CreateDriverShiftRequest(start.AddHours(3), start.AddHours(4)), "admin-create-cancel"); DriverShiftResponse secondShift = (await secondCreatedResponse.Content.ReadFromJsonAsync<DriverShiftResponse>())!; Assert.Equal(HttpStatusCode.OK, (await SendWithKey(admin, HttpMethod.Post, $"/api/v1/drivers/{ownerDriver.Id.Value}/shifts/{secondShift.Id}/cancel", null, "admin-cancel")).StatusCode); Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/drivers/{ownerDriver.Id.Value}/shifts")).StatusCode);

        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); Assert.Equal(ownerDriver.Id, (await db.DriverShifts.AsNoTracking().SingleAsync(x => x.Id == new DriverShiftId(created.Id))).DriverId);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutShiftPermissionIsForbidden()
    {
        (_, HttpClient client) = await AuthenticatedClient(UserType.Driver); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/drivers/me/shifts")).StatusCode);
    }

    private async Task<(Guid UserId, HttpClient Client)> AuthenticatedClient(UserType type, params string[] permissions)
    {
        (Guid userId, string email) = await SeedUser(type, permissions); HttpClient client = fixture.ApiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }); LoginRequest login = new(email, Password, new LoginDeviceRequest("driver-" + Guid.NewGuid().ToString("N"), "Test device", DevicePlatform.Android, "1.0", "15")); TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", login)).Content.ReadFromJsonAsync<TokenResponse>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken); return (userId, client);
    }

    private async Task<(Guid UserId, string Email)> SeedUser(UserType type, string[] permissions)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>(); DateTime now = DateTime.UtcNow; string suffix = Guid.NewGuid().ToString("N"); string email = $"driver-{suffix}@example.com"; User user = User.Create(UserId.New(), type, new Email(email), null, new PasswordHash(hasher.Hash(Password).EncodedHash), now); user.Activate(now); Role role = Role.Create(RoleId.New(), "drivers-role-" + suffix, null, false, now); user.AssignRole(role.Id, now);
        foreach (string name in permissions) { Permission? permission = await db.Permissions.SingleOrDefaultAsync(x => x.Name == name); if (permission is null) { permission = Permission.Create(PermissionId.New(), name, name, null, "drivers", false, now); db.Add(permission); } role.AssignPermission(permission.Id, now); }
        db.AddRange(user, role); await db.SaveChangesAsync(); return (user.Id.Value, email);
    }

    private async Task<Driver> PersistDriver(Guid userId, string name)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); Driver driver = Driver.Create(DriverId.New(), userId, name, EmploymentType.Employee, 2, null, DateTime.UtcNow); db.Drivers.Add(driver); await db.SaveChangesAsync(); return driver;
    }

    private static Task<HttpResponseMessage> SendWithKey(HttpClient client, HttpMethod method, string path, object? body, string key)
    {
        HttpRequestMessage message = new(method, path); message.Headers.Add("Idempotency-Key", key); if (body is not null) message.Content = JsonContent.Create(body); return client.SendAsync(message);
    }
}
