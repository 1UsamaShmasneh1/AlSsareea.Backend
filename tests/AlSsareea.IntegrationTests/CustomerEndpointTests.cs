using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSsareea.Modules.Customers.Application;
using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class CustomerEndpointTests(PostgresFixture fixture)
{
    private const string Password = "Valid-Password-123!";

    [Fact]
    public async Task OwnedProfileAddressPreferencesAndConcurrencyFlowWorks()
    {
        HttpClient client = await AuthenticatedClientAsync();
        HttpResponseMessage createdResponse = await client.PostAsJsonAsync("/api/v1/customers/me", new CreateCustomerRequest(" First ", " Customer ", null));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        CustomerResponse created = (await createdResponse.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.Equal("First Customer", created.DisplayName);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/customers/me", new CreateCustomerRequest("Duplicate", "Profile", null))).StatusCode);

        AddressRequest addressRequest = Address("Home", false);
        HttpResponseMessage addressResponse = await client.PostAsJsonAsync("/api/v1/customers/me/addresses", addressRequest);
        Assert.Equal(HttpStatusCode.Created, addressResponse.StatusCode);
        AddressResponse first = (await addressResponse.Content.ReadFromJsonAsync<AddressResponse>())!;
        Assert.True(first.IsDefault);
        AddressResponse second = (await (await client.PostAsJsonAsync("/api/v1/customers/me/addresses", Address("Work", true))).Content.ReadFromJsonAsync<AddressResponse>())!;
        Assert.True(second.IsDefault);
        AddressResponse refreshedFirst = (await client.GetFromJsonAsync<AddressResponse[]>("/api/v1/customers/me/addresses"))!.Single(x => x.Id == first.Id);
        HttpResponseMessage setDefaultResponse = await client.PutAsJsonAsync($"/api/v1/customers/me/addresses/{first.Id}/default", new ConcurrencyRequest(refreshedFirst.ConcurrencyStamp));
        Assert.Equal(HttpStatusCode.OK, setDefaultResponse.StatusCode);
        AddressResponse firstDefault = (await setDefaultResponse.Content.ReadFromJsonAsync<AddressResponse>())!;
        Assert.True(firstDefault.IsDefault);
        Assert.NotEqual(refreshedFirst.ConcurrencyStamp, firstDefault.ConcurrencyStamp);
        AddressResponse refreshedSecond = (await client.GetFromJsonAsync<AddressResponse[]>("/api/v1/customers/me/addresses"))!.Single(x => x.Id == second.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/customers/me/addresses/{second.Id}?concurrencyStamp={refreshedSecond.ConcurrencyStamp:D}")).StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<AddressResponse[]>("/api/v1/customers/me/addresses"))!, x => x.Id == second.Id);

        PreferencesResponse preferences = (await client.GetFromJsonAsync<PreferencesResponse>("/api/v1/customers/me/preferences"))!;
        var update = new UpdatePreferencesRequest("he", "USD", true, false, true, preferences.ConcurrencyStamp);
        PreferencesResponse updated = (await (await client.PutAsJsonAsync("/api/v1/customers/me/preferences", update)).Content.ReadFromJsonAsync<PreferencesResponse>())!;
        Assert.Equal("he", updated.PreferredLanguage);
        var secondUpdate = new UpdatePreferencesRequest("en", "ILS", false, true, false, updated.ConcurrencyStamp);
        HttpResponseMessage secondUpdateResponse = await client.PutAsJsonAsync("/api/v1/customers/me/preferences", secondUpdate);
        Assert.Equal(HttpStatusCode.OK, secondUpdateResponse.StatusCode);
        PreferencesResponse twiceUpdated = (await secondUpdateResponse.Content.ReadFromJsonAsync<PreferencesResponse>())!;
        Assert.Equal("en", twiceUpdated.PreferredLanguage);
        Assert.NotEqual(updated.ConcurrencyStamp, twiceUpdated.ConcurrencyStamp);
    }

    [Fact]
    public async Task AnotherCustomersAddressIsConcealedAsNotFound()
    {
        HttpClient owner = await AuthenticatedClientAsync();
        await owner.PostAsJsonAsync("/api/v1/customers/me", new CreateCustomerRequest("Owner", "One", null));
        AddressResponse address = (await (await owner.PostAsJsonAsync("/api/v1/customers/me/addresses", Address("Private", false))).Content.ReadFromJsonAsync<AddressResponse>())!;

        HttpClient other = await AuthenticatedClientAsync();
        await other.PostAsJsonAsync("/api/v1/customers/me", new CreateCustomerRequest("Owner", "Two", null));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/customers/me/addresses/{address.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/customers/me/addresses/{address.Id}", Address("Attack", false) with { ConcurrencyStamp = address.ConcurrencyStamp })).StatusCode);
    }

    [Fact]
    public async Task AdministrativeReadsAndStatusChangesRequireTheirExplicitPermissions()
    {
        HttpClient customer = await AuthenticatedClientAsync();
        CustomerResponse profile = (await (await customer.PostAsJsonAsync("/api/v1/customers/me", new CreateCustomerRequest("Admin", "Target", null))).Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/customers?page=1&pageSize=20")).StatusCode);

        HttpClient admin = await AuthenticatedClientAsync(CustomerPermissions.Read, CustomerPermissions.StatusManage, CustomerPermissions.HistoryRead);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/customers?page=1&pageSize=20")).StatusCode);
        var request = new ChangeCustomerStatusRequest((short)AlSsareea.Modules.Customers.Domain.CustomerStatus.Blocked, "manual review", profile.ConcurrencyStamp);
        HttpResponseMessage changedResponse = await admin.PutAsJsonAsync($"/api/v1/admin/customers/{profile.Id}/status", request);
        Assert.Equal(HttpStatusCode.OK, changedResponse.StatusCode);
        Assert.Single((await admin.GetFromJsonAsync<StatusHistoryResponse[]>($"/api/v1/admin/customers/{profile.Id}/status-history"))!);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(params string[] permissions)
    {
        string email = await SeedUserAsync(permissions);
        HttpClient client = fixture.ApiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        LoginRequest login = new(email, Password, new LoginDeviceRequest("customer-" + Guid.NewGuid().ToString("N"), "Test phone", DevicePlatform.Android, "1.0", "15"));
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", login)).Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    private async Task<string> SeedUserAsync(string[] permissions)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        DateTime now = DateTime.UtcNow; string suffix = Guid.NewGuid().ToString("N"); string email = $"customer-{suffix}@example.com";
        User user = User.Create(UserId.New(), UserType.Customer, new Email(email), null, new PasswordHash(hasher.Hash(Password).EncodedHash), now); user.Activate(now);
        Role role = Role.Create(RoleId.New(), "customer-role-" + suffix, null, false, now); user.AssignRole(role.Id, now);
        foreach (string name in permissions)
        {
            Permission? permission = await db.Permissions.SingleOrDefaultAsync(x => x.Name == name);
            if (permission is null) { permission = Permission.Create(PermissionId.New(), name, name, null, "customers", false, now); db.Add(permission); }
            role.AssignPermission(permission.Id, now);
        }
        db.AddRange(user, role); await db.SaveChangesAsync(); return email;
    }

    private static AddressRequest Address(string label, bool isDefault) => new(label, 1, "Jerusalem", null, "Main Street", "12A", null, null, null, null, 31.778, 35.235, "Call on arrival", isDefault, null);
}
