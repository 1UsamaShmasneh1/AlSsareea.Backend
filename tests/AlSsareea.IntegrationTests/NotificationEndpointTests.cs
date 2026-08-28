using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using AlSsareea.Modules.Notifications.Contracts;
using AlSsareea.Modules.Notifications.Domain;
using AlSsareea.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class NotificationEndpointTests(PostgresFixture fixture)
{
    private const string Password = "Valid-Password-123!";
    [Fact] public async Task EndpointsRequireAuthentication() { using HttpClient client = fixture.ApiFactory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/notifications")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/notifications/preferences")).StatusCode); }
    [Fact]
    public async Task UserListsReadsAndCannotAccessAnotherUsersNotification()
    {
        (Guid ownerId, HttpClient owner) = await AuthenticatedClient(); (Guid otherId, HttpClient other) = await AuthenticatedClient(); Notification own = await Persist(ownerId, "own"); Notification foreign = await Persist(otherId, "foreign");
        NotificationListResponse list = (await owner.GetFromJsonAsync<NotificationListResponse>("/api/v1/notifications"))!; Assert.Contains(list.Items, x => x.Id == own.Id.Value); Assert.DoesNotContain(list.Items, x => x.Id == foreign.Id.Value);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/v1/notifications/{own.Id.Value}/read", null)).StatusCode); Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/v1/notifications/{own.Id.Value}/read", null)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/v1/notifications/{foreign.Id.Value}/read", null)).StatusCode); Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/v1/notifications/read-all", null)).StatusCode);
    }
    [Fact]
    public async Task DeviceAndPreferenceLifecycleIsOwnedAndIdempotent()
    {
        (_, HttpClient client) = await AuthenticatedClient(); RegisterDeviceTokenRequest request = new("fcm-test-token-0123456789", (short)PushPlatform.Android, (short)PushProvider.Fcm); HttpResponseMessage created = await client.PostAsJsonAsync("/api/v1/notifications/devices", request); Assert.Equal(HttpStatusCode.Created, created.StatusCode); DeviceTokenResponse token = (await created.Content.ReadFromJsonAsync<DeviceTokenResponse>())!; Assert.DoesNotContain("0123456789", token.TokenMask, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/notifications/devices", request)).StatusCode); Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/v1/notifications/devices/{token.Id}")).StatusCode); Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/v1/notifications/devices/{token.Id}")).StatusCode);
        UpdateNotificationPreferencesRequest preferences = new([new("order_updates", (short)NotificationChannel.Push, false), new("order_updates", (short)NotificationChannel.InApp, true)]); Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/notifications/preferences", preferences)).StatusCode); NotificationPreferencesResponse stored = (await client.GetFromJsonAsync<NotificationPreferencesResponse>("/api/v1/notifications/preferences"))!; Assert.Contains(stored.Items, x => x.Channel == (short)NotificationChannel.Push && !x.Enabled);
    }
    private async Task<Notification> Persist(Guid userId, string body)
    {
        DateTime now = DateTime.UtcNow; Notification value = Notification.Create(NotificationId.New(), userId, Guid.NewGuid(), "order_updates", "test", NotificationChannel.InApp, "en", null, body, now); value.QueueDelivery(null, "inapp", 1, now); await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); NotificationsDbContext db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(); db.Notifications.Add(value); await db.SaveChangesAsync(); return value;
    }
    private async Task<(Guid UserId, HttpClient Client)> AuthenticatedClient()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>(); DateTime now = DateTime.UtcNow; string suffix = Guid.NewGuid().ToString("N"); string email = $"notifications-{suffix}@example.com"; User user = User.Create(UserId.New(), UserType.Customer, new Email(email), null, new PasswordHash(hasher.Hash(Password).EncodedHash), now); user.Activate(now); db.Add(user); await db.SaveChangesAsync(); HttpClient client = fixture.ApiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }); LoginRequest login = new(email, Password, new LoginDeviceRequest("notifications-" + suffix, "Notifications test", DevicePlatform.Android, "1.0", "15")); TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", login)).Content.ReadFromJsonAsync<TokenResponse>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken); return (user.Id.Value, client);
    }
}
