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

    [Fact]
    public async Task CustomerRegistrationCreatesActiveIdentitySessionAndSupportsProfileCreation()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"register-{suffix}@example.com";
        HttpClient client = Client();
        var request = new RegisterCustomerRequest(email, Password, new("registration-" + suffix, "Registration test", DevicePlatform.Android, "1.0", "15"));
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register/customer") { Content = JsonContent.Create(request) };
        message.Headers.Add("Idempotency-Key", "registration-" + suffix);
        HttpResponseMessage response = await client.SendAsync(message);
        TokenResponse tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Customer", tokens.User.UserType);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        HttpResponseMessage profile = await client.PostAsJsonAsync("/api/v1/customers/me/", new { firstName = "Test", lastName = "Customer", dateOfBirth = (DateOnly?)null });
        Assert.Equal(HttpStatusCode.Created, profile.StatusCode);
        TokenResponse refreshed = (await (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken, "registration-" + suffix))).Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(tokens.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", Login(email) with { Device = Login(email).Device with { DeviceIdentifier = "registration-login-" + suffix } })).StatusCode);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        User user = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.SingleAsync(x => x.NormalizedEmail == email);
        Assert.Equal(UserStatus.Active, user.Status); Assert.Equal(UserType.Customer, user.UserType); Assert.NotNull(user.PasswordHash);
    }

    [Fact]
    public async Task RegistrationRejectsDuplicateNormalizedEmailWithoutCreatingAnotherUser()
    {
        string suffix = Guid.NewGuid().ToString("N"); string email = $"duplicate-{suffix}@example.com"; HttpClient client = Client();
        async Task<HttpResponseMessage> Register(string value, string key)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register/customer") { Content = JsonContent.Create(new RegisterCustomerRequest(value, Password, new("duplicate-" + suffix, null, DevicePlatform.Android, null, null))) };
            message.Headers.Add("Idempotency-Key", key); return await client.SendAsync(message);
        }
        Assert.Equal(HttpStatusCode.Created, (await Register(email, "register-first-" + suffix)).StatusCode);
        HttpResponseMessage duplicate = await Register(email.ToUpperInvariant(), "register-second-" + suffix);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.CountAsync(x => x.NormalizedEmail == email));
    }

    [Fact]
    public async Task GoogleCreatesExternalOnlyCustomerThenReusesProviderSubject()
    {
        string suffix = Guid.NewGuid().ToString("N"); string email = $"google-{suffix}@example.com";
        await using var factory = new ApiFactory(fixture.ConnectionString, googleIdentity: new("subject-" + suffix, email, "Google", "Customer"));
        HttpClient client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var request = new GoogleAuthenticationRequest("valid-google-token", null, new("google-device-" + suffix, "Google test", DevicePlatform.Android, "1.0", "15"));
        GoogleAuthenticationResponse first = (await (await client.PostAsJsonAsync("/api/v1/auth/external/google", request)).Content.ReadFromJsonAsync<GoogleAuthenticationResponse>())!;
        GoogleAuthenticationResponse second = (await (await client.PostAsJsonAsync("/api/v1/auth/external/google", request)).Content.ReadFromJsonAsync<GoogleAuthenticationResponse>())!;
        Assert.True(first.IsNewUser); Assert.False(second.IsNewUser); Assert.Equal(first.Tokens.User.Id, second.Tokens.User.Id);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User user = await db.Users.SingleAsync(x => x.Id == new UserId(first.Tokens.User.Id));
        Assert.Null(user.PasswordHash); Assert.Equal(1, await db.ExternalIdentities.CountAsync(x => x.UserId == user.Id));
    }

    [Fact]
    public async Task GoogleNeverAutomaticallyLinksAnExistingEmailAccount()
    {
        string email = await SeedUserAsync(); string suffix = Guid.NewGuid().ToString("N");
        await using var factory = new ApiFactory(fixture.ConnectionString, googleIdentity: new("collision-" + suffix, email.ToUpperInvariant(), null, null));
        HttpClient client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/external/google", new GoogleAuthenticationRequest("valid-google-token", null, new("collision-device-" + suffix, null, DevicePlatform.Android, null, null)));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        ProblemDetailsResponse problem = (await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>())!;
        Assert.Equal(AuthenticationErrorCodes.ExternalLinkRequired, problem.Code);
    }

    [Theory]
    [InlineData("not-an-email", "Secure-Password-123")]
    [InlineData("valid@example.test", "short")]
    public async Task RegistrationRejectsInvalidEmailOrPassword(string email, string password)
    {
        HttpClient client = Client(); string suffix = Guid.NewGuid().ToString("N");
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register/customer") { Content = JsonContent.Create(new RegisterCustomerRequest(email, password, new("validation-" + suffix, null, DevicePlatform.Android, null, null))) };
        message.Headers.Add("Idempotency-Key", "validation-" + suffix);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(message)).StatusCode);
    }

    [Fact]
    public async Task RegistrationRejectsInvalidDeviceAndDuplicateSubmissionWithoutDuplicateUser()
    {
        string suffix = Guid.NewGuid().ToString("N"); string email = $"idempotent-{suffix}@example.com"; HttpClient client = Client();
        async Task<HttpResponseMessage> Send(string device, string key)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register/customer") { Content = JsonContent.Create(new RegisterCustomerRequest(email, Password, new(device, null, DevicePlatform.Android, null, null))) };
            message.Headers.Add("Idempotency-Key", key); return await client.SendAsync(message);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("bad", "invalid-device-" + suffix)).StatusCode);
        string stableKey = "stable-registration-" + suffix;
        Assert.Equal(HttpStatusCode.Created, (await Send("valid-device-" + suffix, stableKey)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("valid-device-" + suffix, stableKey)).StatusCode);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.CountAsync(x => x.NormalizedEmail == email));
    }

    [Fact]
    public async Task PublicRegistrationIgnoresAttemptedPrivilegedUserType()
    {
        string suffix = Guid.NewGuid().ToString("N"); string email = $"type-{suffix}@example.com"; HttpClient client = Client();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register/customer") { Content = JsonContent.Create(new { email, password = Password, userType = "SuperAdministrator", device = new LoginDeviceRequest("type-device-" + suffix, null, DevicePlatform.Android, null, null) }) };
        message.Headers.Add("Idempotency-Key", "type-registration-" + suffix);
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(message)).StatusCode);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        Assert.Equal(UserType.Customer, (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.SingleAsync(x => x.NormalizedEmail == email)).UserType);
    }

    [Fact]
    public async Task GoogleInvalidTokenAndDisabledProviderFailSafely()
    {
        var request = new GoogleAuthenticationRequest("invalid-token", null, new("google-invalid-device", null, DevicePlatform.Android, null, null));
        HttpResponseMessage unavailable = await Client().PostAsJsonAsync("/api/v1/auth/external/google", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        await using var factory = new ApiFactory(fixture.ConnectionString, googleIdentity: new("invalid-subject", "invalid@example.test", null, null));
        HttpClient enabled = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await enabled.PostAsJsonAsync("/api/v1/auth/external/google", request)).StatusCode);
    }

    [Fact]
    public async Task DisabledGoogleLinkedAccountCannotAuthenticate()
    {
        string suffix = Guid.NewGuid().ToString("N"); string email = $"disabled-google-{suffix}@example.com"; string subject = "disabled-" + suffix; DateTime now = DateTime.UtcNow;
        await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); User user = User.CreateExternal(UserId.New(), UserType.Customer, new Email(email), now); user.Disable(now); db.AddRange(user, ExternalIdentity.Create(ExternalIdentityId.New(), user.Id, "google", subject, now)); await db.SaveChangesAsync();
        }
        await using var factory = new ApiFactory(fixture.ConnectionString, googleIdentity: new(subject, email, null, null)); HttpClient client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/external/google", new GoogleAuthenticationRequest("valid-google-token", null, new("disabled-device-" + suffix, null, DevicePlatform.Android, null, null)))).StatusCode);
    }

    [Fact]
    public async Task ConcurrentFirstGoogleSignInCreatesOneUserAndOneExternalIdentity()
    {
        string suffix = Guid.NewGuid().ToString("N"); string email = $"concurrent-google-{suffix}@example.com"; string subject = "concurrent-" + suffix;
        await using var factory = new ApiFactory(fixture.ConnectionString, googleIdentity: new(subject, email, "First", "Last")); HttpClient client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Task<HttpResponseMessage>[] requests = Enumerable.Range(0, 2).Select(index => client.PostAsJsonAsync("/api/v1/auth/external/google", new GoogleAuthenticationRequest("valid-google-token", null, new($"concurrent-device-{suffix}-{index}", null, DevicePlatform.Android, null, null)))).ToArray();
        HttpResponseMessage[] responses = await Task.WhenAll(requests); Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == email)); Assert.Equal(1, await db.ExternalIdentities.CountAsync(x => x.Provider == "google" && x.ProviderSubject == subject));
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
