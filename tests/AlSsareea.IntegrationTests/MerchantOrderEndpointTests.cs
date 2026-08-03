using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;
using AlSsareea.Modules.Orders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class MerchantOrderEndpointTests(PostgresFixture fixture)
{
    private const string Password = "Valid-Password-123!";

    [Fact]
    public async Task MerchantOrderEndpointsRequireAuthenticationAndPermission()
    {
        HttpClient anonymous = fixture.ApiFactory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/merchant/orders?bucket=new")).StatusCode);
        (Guid userId, HttpClient withoutPermission) = await AuthenticatedClient("merchant-orders-no-permission");
        _ = await SeedOperationalOrder(userId);
        Assert.Equal(HttpStatusCode.Forbidden, (await withoutPermission.GetAsync("/api/v1/merchant/orders?bucket=new")).StatusCode);
    }

    [Fact]
    public async Task MerchantLifecycleEndpointsEnforceScopeIdempotencyConcurrencyAndSafeProjection()
    {
        string[] permissions = [OrderPermissions.MerchantRead, OrderPermissions.MerchantHistory, OrderPermissions.MerchantAccept, OrderPermissions.MerchantReject, OrderPermissions.MerchantPrepare, OrderPermissions.MerchantReady];
        (Guid ownerId, HttpClient owner) = await AuthenticatedClient("merchant-orders-owner", permissions);
        (Order order, _, _) = await SeedOperationalOrder(ownerId);

        PagedMerchantOrdersResponse list = (await (await owner.GetAsync("/api/v1/merchant/orders?bucket=new&page=1&pageSize=10")).Content.ReadFromJsonAsync<PagedMerchantOrdersResponse>())!;
        Assert.Contains(list.Items, x => x.OrderId == order.Id.Value);
        MerchantOrderDetails details = (await (await owner.GetAsync($"/api/v1/merchant/orders/{order.Id.Value}")).Content.ReadFromJsonAsync<MerchantOrderDetails>())!;
        string json = System.Text.Json.JsonSerializer.Serialize(details);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);

        AcceptMerchantOrderRequest acceptRequest = new(20, details.ConcurrencyStamp);
        HttpResponseMessage accepted = await Send(owner, HttpMethod.Post, $"/api/v1/merchant/orders/{order.Id.Value}/accept", acceptRequest, "accept-key-0001");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode); MerchantOrderDetails acceptedBody = (await accepted.Content.ReadFromJsonAsync<MerchantOrderDetails>())!;
        Assert.Equal((short)OrderStatus.AcceptedByMerchant, acceptedBody.Status);
        HttpResponseMessage replay = await Send(owner, HttpMethod.Post, $"/api/v1/merchant/orders/{order.Id.Value}/accept", acceptRequest, "accept-key-0001");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        HttpResponseMessage stale = await Send(owner, HttpMethod.Post, $"/api/v1/merchant/orders/{order.Id.Value}/accept", acceptRequest, "accept-key-0002");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        MerchantOrderDetails prepared = await Action(owner, order.Id.Value, "start-preparation", new MerchantOrderTransitionRequest(acceptedBody.ConcurrencyStamp), "prepare-key-0001");
        Assert.Equal((short)OrderStatus.Preparing, prepared.Status);
        MerchantOrderDetails ready = await Action(owner, order.Id.Value, "mark-ready", new MerchantOrderTransitionRequest(prepared.ConcurrencyStamp), "ready-key-00001");
        Assert.Equal((short)OrderStatus.ReadyForPickup, ready.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(owner, HttpMethod.Post, $"/api/v1/merchant/orders/{order.Id.Value}/mark-ready", new MerchantOrderTransitionRequest(ready.ConcurrencyStamp), "ready-key-00002")).StatusCode);

        (Order rejectable, _, _) = await SeedOperationalOrder(ownerId);
        MerchantOrderDetails rejectDetails = (await (await owner.GetAsync($"/api/v1/merchant/orders/{rejectable.Id.Value}")).Content.ReadFromJsonAsync<MerchantOrderDetails>())!;
        HttpResponseMessage rejected = await Send(owner, HttpMethod.Post, $"/api/v1/merchant/orders/{rejectable.Id.Value}/reject", new RejectMerchantOrderRequest((short)MerchantOrderRejectionReason.ItemUnavailable, "Out of stock", rejectDetails.ConcurrencyStamp), "reject-key-001");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        (Guid otherOwner, HttpClient other) = await AuthenticatedClient("merchant-orders-other", OrderPermissions.MerchantRead);
        _ = await SeedOperationalOrder(otherOwner);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/merchant/orders/{order.Id.Value}")).StatusCode);
    }

    [Fact]
    public async Task ValidationAndHistoryPermissionsUseStableProblemResponses()
    {
        (Guid ownerId, HttpClient owner) = await AuthenticatedClient("merchant-orders-validation", OrderPermissions.MerchantRead, OrderPermissions.MerchantAccept, OrderPermissions.MerchantHistory);
        (Order order, _, _) = await SeedOperationalOrder(ownerId);
        MerchantOrderDetails details = (await (await owner.GetAsync($"/api/v1/merchant/orders/{order.Id.Value}")).Content.ReadFromJsonAsync<MerchantOrderDetails>())!;
        HttpResponseMessage invalid = await Send(owner, HttpMethod.Post, $"/api/v1/merchant/orders/{order.Id.Value}/accept", new AcceptMerchantOrderRequest(0, details.ConcurrencyStamp), "invalid-key-001");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        string problem = await invalid.Content.ReadAsStringAsync(); Assert.Contains(OrderErrorCodes.PreparationTimeInvalid, problem, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/merchant/orders/{order.Id.Value}/history")).StatusCode);
    }

    private static async Task<MerchantOrderDetails> Action(HttpClient client, Guid orderId, string action, MerchantOrderTransitionRequest request, string key)
    {
        HttpResponseMessage response = await Send(client, HttpMethod.Post, $"/api/v1/merchant/orders/{orderId}/{action}", request, key);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<MerchantOrderDetails>())!;
    }

    private static async Task<HttpResponseMessage> Send<T>(HttpClient client, HttpMethod method, string path, T body, string key)
    {
        using HttpRequestMessage request = new(method, path) { Content = JsonContent.Create(body) }; request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private async Task<(Order Order, Merchant Merchant, MerchantBranch Branch)> SeedOperationalOrder(Guid ownerId)
    {
        DateTime now = DateTime.UtcNow; Merchant merchant = Merchant.Create(MerchantId.New(), "Legal", "Merchant", null, null, null, $"merchant-{Guid.NewGuid():N}@example.com", "+970599000000", ownerId, now); merchant.Activate(now.AddSeconds(1));
        MerchantBranch branch = MerchantBranch.Create(MerchantBranchId.New(), merchant.Id, "Branch", null, "+970599000001", null, BranchAddress.Create("City", null, "Street", "1", null), new GeoCoordinate(31.9, 35.2), "Asia/Jerusalem", true, now); branch.Activate(true, now.AddSeconds(1));
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); MerchantsDbContext merchants = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>(); merchants.AddRange(merchant, branch); await merchants.SaveChangesAsync();
        Order order = CreateOrder(merchant.Id.Value, branch.Id.Value, now.AddSeconds(2)); OrdersDbContext orders = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); OrderRepository repository = new(orders, new FixedClock(now.AddSeconds(5))); Assert.Equal(OrderCreatePersistenceResult.Created, await repository.CreateAsync(order, order.CustomerId, "order.create", Hash(Guid.NewGuid().ToString()), Hash(Guid.NewGuid().ToString()), [], default));
        return (order, merchant, branch);
    }

    private static Order CreateOrder(Guid merchantId, Guid branchId, DateTime now)
    {
        Guid customer = Guid.NewGuid(); OrderItemInput item = new(Guid.NewGuid(), 1, null, "Item", null, null, 1, 1000, 0, 0, 1000, 1000, 0, 1000, null, []);
        Order order = Order.Create(OrderId.New(), Guid.NewGuid().ToString("N").ToUpperInvariant(), customer, merchantId, branchId, Guid.NewGuid(), OrderType.Restaurant, new(1000, 0, 0, 0, 0, 100, 50, 25, 0, 25, 1200, "ILS", null, now), new(customer, "Customer", "+970599123456", "ar"), new(Guid.NewGuid(), "Home", "City", null, "Street", "1", null, null, "Door", null, null, null, null), new(merchantId, branchId, "Merchant", "Branch", null, null), [item], null, null, now);
        order.MarkPaymentAuthorized(now.AddSeconds(1)); order.Submit(now.AddSeconds(2)); return order;
    }

    private async Task<(Guid UserId, HttpClient Client)> AuthenticatedClient(string rolePrefix, params string[] permissions)
    {
        (Guid userId, string email) = await SeedUser(rolePrefix, permissions); HttpClient client = fixture.ApiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        LoginRequest login = new(email, Password, new LoginDeviceRequest("merchant-orders-" + Guid.NewGuid().ToString("N"), "Test", DevicePlatform.Android, "1.0", "15"));
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", login)).Content.ReadFromJsonAsync<TokenResponse>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken); return (userId, client);
    }

    private async Task<(Guid UserId, string Email)> SeedUser(string rolePrefix, string[] permissions)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>(); DateTime now = DateTime.UtcNow; string suffix = Guid.NewGuid().ToString("N"); string email = $"merchant-orders-{suffix}@example.com";
        User user = User.Create(UserId.New(), UserType.MerchantOwner, new Email(email), null, new PasswordHash(hasher.Hash(Password).EncodedHash), now); user.Activate(now); Role role = Role.Create(RoleId.New(), rolePrefix + "-" + suffix, null, false, now); user.AssignRole(role.Id, now);
        foreach (string name in permissions) { Permission? permission = await db.Permissions.SingleOrDefaultAsync(x => x.Name == name); if (permission is null) { permission = Permission.Create(PermissionId.New(), name, name, null, "orders", false, now); db.Add(permission); } role.AssignPermission(permission.Id, now); }
        db.AddRange(user, role); await db.SaveChangesAsync(); return (user.Id.Value, email);
    }
    private static string Hash(string value) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    private sealed class FixedClock(DateTime utcNow) : IClock { public DateTime UtcNow { get; } = utcNow; }
}
