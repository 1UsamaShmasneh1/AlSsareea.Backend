using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class AuthenticationEndpointTests(PostgresFixture fixture)
{
    private const string Password = "Secure-Password-123";

    [Fact]
    public async Task LoginMeSessionsAndLogoutWorkWithoutPersistingRawRefreshToken()
    {
        string email = await SeedUserAsync(); HttpClient client = Client();
        HttpResponseMessage login = await client.PostAsJsonAsync("/api/v1/auth/login", Login(email), CancellationToken.None);
        TokenResponse tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(HttpStatusCode.OK, login.StatusCode); Assert.Equal("no-store", login.Headers.CacheControl?.ToString()); Assert.NotEmpty(tokens.RefreshToken);
        await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            List<RefreshToken> persistedTokens = await db.RefreshTokens.AsNoTracking().ToListAsync();
            Assert.DoesNotContain(persistedTokens, x => x.TokenHash.Value == tokens.RefreshToken);
            Assert.Equal(64, (await db.RefreshTokens.SingleAsync(x => x.LoginSessionId == new LoginSessionId(tokens.SessionId))).TokenHash.Value.Length);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/sessions")).StatusCode);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout"); logout.Headers.Add("Idempotency-Key", "logout-key-12345678");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(logout)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task UnknownIdentifierAndWrongPasswordReturnEquivalentPublicError()
    {
        string email = await SeedUserAsync(); HttpClient client = Client();
        HttpResponseMessage unknown = await client.PostAsJsonAsync("/api/v1/auth/login", Login("unknown-" + email));
        LoginRequest wrong = Login(email) with { Password = "Wrong-Password-123" };
        HttpResponseMessage invalid = await client.PostAsJsonAsync("/api/v1/auth/login", wrong);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        ProblemDetailsResponse unknownProblem = (await unknown.Content.ReadFromJsonAsync<ProblemDetailsResponse>())!;
        ProblemDetailsResponse invalidProblem = (await invalid.Content.ReadFromJsonAsync<ProblemDetailsResponse>())!;
        Assert.Equal((unknownProblem.Status, unknownProblem.Title, unknownProblem.Code), (invalidProblem.Status, invalidProblem.Title, invalidProblem.Code));
    }

    [Fact]
    public async Task RefreshRotationAllowsOneConcurrentSuccessAndReplayRevokesSession()
    {
        string email = await SeedUserAsync(); HttpClient client = Client(); TokenResponse tokens = (await (await client.PostAsJsonAsync("/api/v1/auth/login", Login(email))).Content.ReadFromJsonAsync<TokenResponse>())!;
        var request = new RefreshRequest(tokens.RefreshToken, "device-functional-tests");
        Task<HttpResponseMessage> first = client.PostAsJsonAsync("/api/v1/auth/refresh", request);
        Task<HttpResponseMessage> second = client.PostAsJsonAsync("/api/v1/auth/refresh", request);
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);
        string responseSummary = string.Join(" | ", await Task.WhenAll(responses.Select(async x => $"{(int)x.StatusCode}:{await x.Content.ReadAsStringAsync()}")));
        Assert.True(responses.Count(x => x.StatusCode == HttpStatusCode.OK) == 1, responseSummary); Assert.True(responses.Count(x => x.StatusCode == HttpStatusCode.Unauthorized) == 1, responseSummary);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); LoginSession session = await db.LoginSessions.SingleAsync(x => x.Id == new LoginSessionId(tokens.SessionId)); Assert.Equal(SessionState.Revoked, session.State); Assert.True(await db.RefreshTokens.AnyAsync(x => x.LoginSessionId == session.Id && x.ReplayDetectedUtc != null));
    }

    [Fact]
    public async Task OtpCanBeVerifiedOnceAndIdempotencyDoesNotReturnCodeTwice()
    {
        HttpClient client = Client(); var request = new OtpChallengeRequest("otp@example.com", OtpPurpose.Login, "device-otp-tests");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "otp-key-123456789");
        OtpChallengeResponse created = (await (await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", request)).Content.ReadFromJsonAsync<OtpChallengeResponse>())!;
        Assert.NotNull(created.DevelopmentCode);
        OtpChallengeResponse duplicate = (await (await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", request)).Content.ReadFromJsonAsync<OtpChallengeResponse>())!;
        Assert.Null(duplicate.DevelopmentCode); Assert.Equal(created.ChallengeId, duplicate.ChallengeId);
        var verify = new OtpVerifyRequest(created.DevelopmentCode!, "device-otp-tests");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/auth/otp/challenges/{created.ChallengeId}/verify", verify)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/v1/auth/otp/challenges/{created.ChallengeId}/verify", verify)).StatusCode);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); Assert.DoesNotContain(created.DevelopmentCode!, (await db.OtpChallenges.SingleAsync(x => x.Id == new OtpChallengeId(created.ChallengeId))).CodeHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingAndModifiedAccessTokensAreRejected()
    {
        HttpClient client = Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0.");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task FailedLoginsLockAccountAndSuccessResetsCounter()
    {
        string email = await SeedUserAsync(); HttpClient client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", Login(email) with { Password = "Wrong-Password-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", Login(email))).StatusCode);
        await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            Assert.Equal(0, (await db.Users.SingleAsync(x => x.NormalizedEmail == email)).FailedLoginCount);
        }

        for (int attempt = 0; attempt < 5; attempt++)
            _ = await client.PostAsJsonAsync("/api/v1/auth/login", Login(email) with { Password = "Wrong-Password-123" });
        await using AsyncServiceScope lockedScope = fixture.ApiFactory.Services.CreateAsyncScope();
        User locked = await lockedScope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters().SingleAsync(x => x.NormalizedEmail == email);
        Assert.Equal(UserStatus.Locked, locked.Status); Assert.NotNull(locked.LockoutEndUtc);
    }

    [Fact]
    public async Task ConcurrentFailedLoginsUpdateLockoutAtomically()
    {
        string email = await SeedUserAsync(); HttpClient client = Client(); LoginRequest wrong = Login(email) with { Password = "Wrong-Password-123" };
        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.PostAsJsonAsync("/api/v1/auth/login", wrong)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); User user = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters().SingleAsync(x => x.NormalizedEmail == email);
        Assert.Equal(5, user.FailedLoginCount); Assert.Equal(UserStatus.Locked, user.Status);
    }

    [Fact]
    public async Task UserCannotRevokeAnotherUsersSessionAndMissingPermissionIsForbidden()
    {
        string ownerEmail = await SeedUserAsync(); string otherEmail = await SeedUserAsync(withPermissions: false); HttpClient client = Client();
        TokenResponse owner = (await (await client.PostAsJsonAsync("/api/v1/auth/login", Login(ownerEmail))).Content.ReadFromJsonAsync<TokenResponse>())!;
        TokenResponse other = (await (await client.PostAsJsonAsync("/api/v1/auth/login", Login(otherEmail))).Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        using var revoke = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/sessions/{other.SessionId}"); revoke.Headers.Add("Idempotency-Key", "ownership-key-123456");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(revoke)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/auth/sessions")).StatusCode);
    }

    [Fact]
    public async Task LogoutAllRevokesEverySessionAndRotatesSecurityStamp()
    {
        string email = await SeedUserAsync(); HttpClient client = Client();
        TokenResponse first = (await (await client.PostAsJsonAsync("/api/v1/auth/login", Login(email))).Content.ReadFromJsonAsync<TokenResponse>())!;
        TokenResponse second = (await (await client.PostAsJsonAsync("/api/v1/auth/login", Login(email) with { Device = Login(email).Device with { DeviceIdentifier = "second-device" } })).Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        using var logoutAll = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout-all"); logoutAll.Headers.Add("Idempotency-Key", "logout-all-key-12345");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(logoutAll)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.DoesNotContain(await db.LoginSessions.Where(x => x.UserId == new UserId(second.User.Id)).ToListAsync(), x => x.State == SessionState.Active);
    }

    [Fact]
    public async Task OtpVerificationIsAtomicAndIdempotencyPayloadConflictIsRejected()
    {
        HttpClient client = Client(); client.DefaultRequestHeaders.Add("Idempotency-Key", "otp-concurrent-key-123");
        OtpChallengeResponse created = (await (await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new OtpChallengeRequest("atomic@example.com", OtpPurpose.Login, "atomic-device"))).Content.ReadFromJsonAsync<OtpChallengeResponse>())!;
        HttpResponseMessage conflict = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new OtpChallengeRequest("different@example.com", OtpPurpose.Login, "atomic-device"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var verify = new OtpVerifyRequest(created.DevelopmentCode!, "atomic-device");
        HttpResponseMessage[] responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/v1/auth/otp/challenges/{created.ChallengeId}/verify", verify), client.PostAsJsonAsync($"/api/v1/auth/otp/challenges/{created.ChallengeId}/verify", verify));
        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK)); Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task LoginRateLimitReturnsRetryAfter()
    {
        await using var rateLimitedFactory = new ApiFactory(fixture.ConnectionString, loginPermitLimit: 2);
        HttpClient client = rateLimitedFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        HttpResponseMessage response = null!;
        for (int request = 0; request < 3; request++)
        {
            client.DefaultRequestHeaders.Remove("X-Device-Identifier"); client.DefaultRequestHeaders.Add("X-Device-Identifier", "outer-partition-" + request);
            response = await client.PostAsJsonAsync("/api/v1/auth/login", Login(request % 2 == 0 ? "Rate-Limit@Example.com" : "rate-limit@example.com"));
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode); Assert.True(response.Headers.Contains("Retry-After"));
    }

    private async Task<string> SeedUserAsync(bool withPermissions = true)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>(); DateTime now = DateTime.UtcNow; string suffix = Guid.NewGuid().ToString("N"); string email = $"auth-{suffix}@example.com";
        User user = User.Create(UserId.New(), UserType.Customer, new Email(email), null, new PasswordHash(hasher.Hash(Password).EncodedHash), now); user.Activate(now);
        Role role = Role.Create(RoleId.New(), "auth-role-" + suffix, null, false, now);
        Permission? read = await db.Permissions.SingleOrDefaultAsync(x => x.Name == AuthenticationPolicies.SessionsRead);
        Permission? revoke = await db.Permissions.SingleOrDefaultAsync(x => x.Name == AuthenticationPolicies.SessionsRevoke);
        read ??= Permission.Create(PermissionId.New(), AuthenticationPolicies.SessionsRead, "Read sessions", null, "identity", false, now);
        revoke ??= Permission.Create(PermissionId.New(), AuthenticationPolicies.SessionsRevoke, "Revoke sessions", null, "identity", false, now);
        user.AssignRole(role.Id, now);
        if (withPermissions) { role.AssignPermission(read.Id, now); role.AssignPermission(revoke.Id, now); }
        db.AddRange(user, role);
        if (db.Entry(read).State == EntityState.Detached) db.Add(read);
        if (db.Entry(revoke).State == EntityState.Detached) db.Add(revoke);
        await db.SaveChangesAsync(); return email;
    }

    private HttpClient Client() => fixture.ApiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    private static LoginRequest Login(string email) => new(email, Password, new LoginDeviceRequest("device-functional-tests", "Test phone", DevicePlatform.Android, "1.0.0", "15"));

    private sealed record ProblemDetailsResponse(int Status, string Title, string Code);
}
