using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using AlSsareea.Modules.Merchants.Application;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Pricing.Contracts;
using AlSsareea.Modules.Promotions.Application;
using AlSsareea.Modules.Promotions.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class PromotionEndpointTests(PostgresFixture fixture)
{
    private const string Password = "Valid-Password-123!";

    [Fact]
    public async Task MerchantScopedCouponFlowEnforcesMembershipEvaluatesAndHandlesConcurrency()
    {
        (Guid ownerId, HttpClient owner) = await AuthenticatedClient(
            UserType.MerchantOwner,
            "promotion-owner",
            MerchantPermissions.Create,
            PromotionPermissions.Create,
            PromotionPermissions.View,
            PromotionPermissions.Activate,
            PromotionPermissions.Suspend,
            PromotionPermissions.Evaluate);
        var merchantRequest = new CreateMerchantRequest("Promotion Merchant", "Promotion Merchant", null, null, null, "promotion-merchant@example.com", "+970599100000", ownerId);
        HttpResponseMessage merchantResponse = await owner.PostAsJsonAsync("/api/v1/merchants", merchantRequest);
        Assert.Equal(HttpStatusCode.Created, merchantResponse.StatusCode);
        MerchantResponse merchant = (await merchantResponse.Content.ReadFromJsonAsync<MerchantResponse>())!;

        CreatePromotionRequest request = Request(merchant.Id);
        HttpResponseMessage createdResponse = await owner.PostAsJsonAsync("/api/v1/promotions", request);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        PromotionResponse created = (await createdResponse.Content.ReadFromJsonAsync<PromotionResponse>())!;

        (_, HttpClient other) = await AuthenticatedClient(
            UserType.MerchantOwner,
            "promotion-other",
            PromotionPermissions.Create,
            PromotionPermissions.Evaluate);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/v1/promotions", Request(merchant.Id))).StatusCode);

        HttpResponseMessage activatedResponse = await owner.PostAsJsonAsync($"/api/v1/promotions/{created.Id}/activate", new PromotionActionRequest(created.ConcurrencyStamp));
        Assert.Equal(HttpStatusCode.OK, activatedResponse.StatusCode);
        PromotionResponse activated = (await activatedResponse.Content.ReadFromJsonAsync<PromotionResponse>())!;

        var pricing = new PricingBreakdownDto("ILS", 2000, 300, 0, 0, 0, 0, 0, 2300);
        var evaluation = new EvaluatePromotionsRequest(null, merchant.Id, null, pricing, null, [], " phase9b ", new UsageContext(0, 0, 0, true));
        HttpResponseMessage evaluationResponse = await owner.PostAsJsonAsync("/api/v1/promotions/evaluate", evaluation);
        Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/v1/promotions/evaluate", evaluation)).StatusCode);
        PromotionEvaluationResponse result = (await evaluationResponse.Content.ReadFromJsonAsync<PromotionEvaluationResponse>())!;
        Assert.Contains(result.Snapshots, x => x.PromotionId == created.Id && x.NormalizedCouponCode == "PHASE9B");

        HttpResponseMessage stale = await owner.PostAsJsonAsync($"/api/v1/promotions/{created.Id}/suspend", new PromotionActionRequest(created.ConcurrencyStamp));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.NotEqual(created.ConcurrencyStamp, activated.ConcurrencyStamp);
    }

    [Fact]
    public async Task PromotionCreationWithoutPermissionIsForbidden()
    {
        (_, HttpClient client) = await AuthenticatedClient(UserType.Administrator, "promotion-no-permission");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/promotions", Request(Guid.NewGuid()))).StatusCode);
    }

    private static CreatePromotionRequest Request(Guid merchantId)
    {
        DateTime now = DateTime.UtcNow;
        return new CreatePromotionRequest(
            "phase-9b-" + Guid.NewGuid().ToString("N"),
            new LocalizedTextRequest("عرض", null, "Promotion"),
            null,
            1,
            10,
            1,
            null,
            new FundingRequest(2, 0, 10000),
            now.AddMinutes(-5),
            now.AddDays(1),
            new UsageLimitsRequest(100, 2, 100000, 1),
            new EligibilityRequest(1000, null, false),
            new ScopeRequest(2, [merchantId]),
            new BenefitRequest(2, "ILS", 1000, 500),
            "phase9b");
    }

    private async Task<(Guid UserId, HttpClient Client)> AuthenticatedClient(UserType type, string rolePrefix, params string[] permissions)
    {
        (Guid userId, string email) = await SeedUser(type, rolePrefix, permissions);
        HttpClient client = fixture.ApiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        LoginRequest login = new(email, Password, new LoginDeviceRequest("promotion-" + Guid.NewGuid().ToString("N"), "Test phone", DevicePlatform.Android, "1.0", "15"));
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
        string email = $"promotion-{suffix}@example.com";
        User user = User.Create(UserId.New(), type, new Email(email), null, new PasswordHash(hasher.Hash(Password).EncodedHash), now);
        user.Activate(now);
        Role role = Role.Create(RoleId.New(), rolePrefix + "-" + suffix, null, false, now);
        user.AssignRole(role.Id, now);
        foreach (string name in permissions)
        {
            Permission? permission = await db.Permissions.SingleOrDefaultAsync(x => x.Name == name);
            if (permission is null)
            {
                permission = Permission.Create(PermissionId.New(), name, name, null, "promotions", false, now);
                db.Add(permission);
            }
            role.AssignPermission(permission.Id, now);
        }
        db.AddRange(user, role);
        await db.SaveChangesAsync();
        return (user.Id.Value, email);
    }
}
