using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using AlSsareea.Modules.Merchants.Application;
using AlSsareea.Modules.Merchants.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class MerchantEndpointTests(PostgresFixture fixture)
{
    private const string Password = "Valid-Password-123!";

    [Fact]
    public async Task ScheduleOverrideDeleteEndpointIsDiscoverableWithoutRequestBody()
    {
        HttpClient client = fixture.ApiFactory.CreateClient();
        Guid merchantId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();
        Guid concurrencyStamp = Guid.NewGuid();

        using HttpResponseMessage response = await client.DeleteAsync(
            $"/api/v1/merchants/{merchantId}/branches/{branchId}/schedule-overrides/{overrideId}?concurrencyStamp={concurrencyStamp}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerFlowAndPlatformActivationRespectBothPermissionAndMembership()
    {
        (Guid ownerId, HttpClient owner) = await AuthenticatedClient(
            UserType.MerchantOwner,
            "merchant-owner",
            MerchantPermissions.Create,
            MerchantPermissions.View,
            MerchantPermissions.BranchesView,
            MerchantPermissions.BranchesManage);
        var create = new CreateMerchantRequest("Legal Shop", "Shop", null, null, null, "shop@example.com", "+970599000000", ownerId);
        HttpResponseMessage createdResponse = await owner.PostAsJsonAsync("/api/v1/merchants", create);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        MerchantResponse merchant = (await createdResponse.Content.ReadFromJsonAsync<MerchantResponse>())!;

        var branchRequest = new CreateMerchantBranchRequest(
            "Central",
            "CTR",
            "+970599000001",
            "branch@example.com",
            new BranchAddressRequest("Ramallah", null, "Main Street", "1", null),
            new CoordinateRequest(31.9, 35.2),
            "Asia/Jerusalem",
            true);
        MerchantBranchResponse branch = (await (await owner.PostAsJsonAsync($"/api/v1/merchants/{merchant.Id}/branches", branchRequest)).Content.ReadFromJsonAsync<MerchantBranchResponse>())!;
        Assert.True(branch.IsPrimary);

        (_, HttpClient other) = await AuthenticatedClient(UserType.MerchantOwner, "merchant-other", MerchantPermissions.View, MerchantPermissions.BranchesView);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/merchants/{merchant.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/merchants/{merchant.Id}/branches/{branch.Id}")).StatusCode);

        (_, HttpClient platform) = await AuthenticatedClient(UserType.Administrator, "platform-admin", MerchantPermissions.LifecycleManage, MerchantPermissions.View);
        HttpResponseMessage activated = await platform.PostAsJsonAsync($"/api/v1/merchants/{merchant.Id}/activate", new MerchantEmployeeActionRequest(merchant.ConcurrencyStamp));
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
    }

    [Fact]
    public async Task MissingGlobalPermissionIsForbiddenBeforeResourceScope()
    {
        (Guid ownerId, HttpClient owner) = await AuthenticatedClient(UserType.MerchantOwner, "merchant-no-permissions");
        var request = new CreateMerchantRequest("Legal", "Display", null, null, null, "owner@example.com", "+970599000000", ownerId);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/v1/merchants", request)).StatusCode);
    }

    private async Task<(Guid UserId, HttpClient Client)> AuthenticatedClient(UserType type, string rolePrefix, params string[] permissions)
    {
        (Guid userId, string email) = await SeedUser(type, rolePrefix, permissions);
        HttpClient client = fixture.ApiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        LoginRequest login = new(email, Password, new LoginDeviceRequest("merchant-" + Guid.NewGuid().ToString("N"), "Test phone", DevicePlatform.Android, "1.0", "15"));
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", login)).Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return (userId, client);
    }

    private async Task<(Guid UserId, string Email)> SeedUser(UserType type, string rolePrefix, string[] permissions)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        DateTime now = DateTime.UtcNow;
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"merchant-{suffix}@example.com";
        User user = User.Create(UserId.New(), type, new Email(email), null, new PasswordHash(hasher.Hash(Password).EncodedHash), now);
        user.Activate(now);
        string roleName = rolePrefix == "platform-admin" ? rolePrefix : rolePrefix + "-" + suffix;
        Role role = Role.Create(RoleId.New(), roleName, null, false, now);
        user.AssignRole(role.Id, now);
        foreach (string name in permissions)
        {
            Permission? permission = await db.Permissions.SingleOrDefaultAsync(x => x.Name == name);
            if (permission is null)
            {
                permission = Permission.Create(PermissionId.New(), name, name, null, "merchants", false, now);
                db.Add(permission);
            }
            role.AssignPermission(permission.Id, now);
        }
        db.AddRange(user, role);
        await db.SaveChangesAsync();
        return (user.Id.Value, email);
    }
}
