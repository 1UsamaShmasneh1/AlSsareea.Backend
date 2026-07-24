using System.Data.Common;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class IdentityPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MigrationCreatesSchemaHistoryAndAllTablesWithoutPendingChanges()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        string[] expected = ["users", "roles", "permissions", "user_roles", "role_permissions", "devices", "login_sessions", "refresh_tokens", "password_history", "login_history", "__ef_migrations_history"];
        List<string> actual = await QueryStringsAsync(db, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'identity'");
        Assert.All(expected, table => Assert.Contains(table, actual));
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), x => x.EndsWith("_InitializeIdentityDomain", StringComparison.Ordinal));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task UserRoundTripsWithStrongIdsEnumsAndUtcTimestamp()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User user = NewUser(); db.Users.Add(user); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        User persisted = await db.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id);
        Assert.Equal(user.Id, persisted.Id); Assert.Equal(UserType.Customer, persisted.UserType); Assert.Equal(DateTimeKind.Utc, persisted.CreatedUtc.Kind);
        Assert.Equal("person-" + user.Id.Value.ToString("N") + "@example.com", persisted.NormalizedEmail);
    }

    [Fact]
    public async Task UserEmailAndPhoneRemainUniqueIncludingSoftDeletedUsers()
    {
        string suffix = Guid.NewGuid().ToString("N"); Email email = new($"unique-{suffix}@example.com"); PhoneNumber phone = new("+1" + Random.Shared.NextInt64(1000000000, 9999999999));
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User first = NewUser(email, phone); db.Users.Add(first); await db.SaveChangesAsync(); first.SoftDelete(Now.AddMinutes(1)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.Users.Add(NewUser(email, new PhoneNumber("+1" + Random.Shared.NextInt64(1000000000, 9999999999)))); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Users.Add(NewUser(new Email($"other-{suffix}@example.com"), phone)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SoftDeleteFilterCanBeExplicitlyBypassed()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); User user = NewUser(); db.Add(user); await db.SaveChangesAsync(); user.SoftDelete(Now.AddMinutes(1)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Null(await db.Users.SingleOrDefaultAsync(x => x.Id == user.Id)); Assert.NotNull(await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == user.Id));
    }

    [Fact]
    public async Task AggregateUniqueConstraintsRejectDuplicates()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        string name = "Role-" + Guid.NewGuid().ToString("N"); db.Roles.Add(Role.Create(RoleId.New(), name, null, false, Now)); await db.SaveChangesAsync(); db.Roles.Add(Role.Create(RoleId.New(), name.ToLowerInvariant(), null, false, Now)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        string permissionName = "identity.test." + Guid.NewGuid().ToString("N"); db.Permissions.Add(Permission.Create(PermissionId.New(), permissionName, "Test", null, "identity", false, Now)); await db.SaveChangesAsync(); db.Permissions.Add(Permission.Create(PermissionId.New(), permissionName, "Test 2", null, "identity", false, Now)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task JoinAndDeviceCompositeKeysEnforceUniqueness()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); User user = NewUser(); Role role = Role.Create(RoleId.New(), "role-" + Guid.NewGuid().ToString("N"), null, false, Now); Permission permission = Permission.Create(PermissionId.New(), "identity.test." + Guid.NewGuid().ToString("N"), "Test", null, "identity", false, Now); user.AssignRole(role.Id, Now); role.AssignPermission(permission.Id, Now); user.RegisterDevice(DeviceId.New(), new DeviceIdentifier("device-" + Guid.NewGuid().ToString("N")), DevicePlatform.Android, null, null, null, Now); db.AddRange(user, role, permission); await db.SaveChangesAsync();
        Assert.Single(await db.UserRoles.Where(x => x.UserId == user.Id).ToListAsync()); Assert.Single(await db.RolePermissions.Where(x => x.RoleId == role.Id).ToListAsync()); Assert.Single(await db.Devices.Where(x => x.UserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task SameDeviceIdentifierIsAllowedForDifferentUsers()
    {
        string identifier = "device-" + Guid.NewGuid().ToString("N"); User first = NewUser(); User second = NewUser(); first.RegisterDevice(DeviceId.New(), new DeviceIdentifier(identifier), DevicePlatform.Ios, null, null, null, Now); second.RegisterDevice(DeviceId.New(), new DeviceIdentifier(identifier), DevicePlatform.Ios, null, null, null, Now);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); db.AddRange(first, second); await db.SaveChangesAsync(); Assert.Equal(2, await db.Devices.CountAsync(x => x.DeviceIdentifier == new DeviceIdentifier(identifier)));
    }

    [Fact]
    public async Task RefreshTokenHashIsUniqueAndLifecyclePersists()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); User user = NewUser(); LoginSession session = LoginSession.Start(LoginSessionId.New(), user.Id, null, Now, Now.AddDays(1)); db.AddRange(user, session); await db.SaveChangesAsync(); RefreshTokenHash hash = new(new string('a', 32) + Guid.NewGuid().ToString("N")); RefreshToken token = RefreshToken.Create(RefreshTokenId.New(), user.Id, null, session.Id, hash, user.SecurityStamp, Now, Now.AddDays(1)); token.Revoke("test", Now.AddMinutes(1)); db.Add(token); await db.SaveChangesAsync(); db.Add(RefreshToken.Create(RefreshTokenId.New(), user.Id, null, session.Id, hash, user.SecurityStamp, Now, Now.AddDays(1))); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ConflictingUpdatesThrowOptimisticConcurrencyException()
    {
        User user = NewUser(); await using (AsyncServiceScope setup = fixture.ApiFactory.Services.CreateAsyncScope()) { IdentityDbContext db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>(); db.Add(user); await db.SaveChangesAsync(); }
        await using AsyncServiceScope firstScope = fixture.ApiFactory.Services.CreateAsyncScope(); await using AsyncServiceScope secondScope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext first = firstScope.ServiceProvider.GetRequiredService<IdentityDbContext>(); IdentityDbContext second = secondScope.ServiceProvider.GetRequiredService<IdentityDbContext>(); User firstCopy = await first.Users.SingleAsync(x => x.Id == user.Id); User secondCopy = await second.Users.SingleAsync(x => x.Id == user.Id); firstCopy.Activate(Now.AddMinutes(1)); secondCopy.Activate(Now.AddMinutes(2)); await first.SaveChangesAsync(); await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task HistoryIsAppendOnlyAndUsesInetAndTimestamptzMappings()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); LoginHistory history = LoginHistory.Create(LoginHistoryId.New(), null, null, null, new string('c', 64), LoginResult.Failed, LoginFailureReason.InvalidCredentials, System.Net.IPAddress.Loopback, "agent", Now, "correlation"); db.Add(history); await db.SaveChangesAsync(); db.Remove(history); await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        List<string> types = await QueryStringsAsync(db, "SELECT data_type FROM information_schema.columns WHERE table_schema='identity' AND ((table_name='login_history' AND column_name='ip_address') OR (table_name='users' AND column_name='created_utc')) ORDER BY data_type"); Assert.Contains("inet", types); Assert.Contains("timestamp with time zone", types);
    }

    [Fact]
    public async Task EveryIdentityForeignKeyUsesRestrictAndOnlyOwnedModuleSchemasAreCreated()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        long cascades = await ScalarLongAsync(db, "SELECT count(*) FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace WHERE n.nspname='identity' AND c.contype='f' AND c.confdeltype='c'"); Assert.Equal(0, cascades);
        List<string> schemas = await QueryStringsAsync(db, "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('identity','customers','public','information_schema','pg_catalog','pg_toast','topology','tiger','tiger_data') AND schema_name NOT LIKE 'pg_temp_%' AND schema_name NOT LIKE 'pg_toast_temp_%'"); Assert.Empty(schemas);
    }

    private static User NewUser(Email? email = null, PhoneNumber? phone = null) { UserId id = UserId.New(); return User.Create(id, UserType.Customer, email ?? new Email($"person-{id.Value:N}@example.com"), phone, new PasswordHash("argon2id$v=19$integration-test-hash"), Now); }
    private static async Task<List<string>> QueryStringsAsync(IdentityDbContext db, string sql) { DbConnection connection = db.Database.GetDbConnection(); await connection.OpenAsync(); await using DbCommand command = connection.CreateCommand(); command.CommandText = sql; await using DbDataReader reader = await command.ExecuteReaderAsync(); var values = new List<string>(); while (await reader.ReadAsync()) values.Add(reader.GetString(0)); await connection.CloseAsync(); return values; }
    private static async Task<long> ScalarLongAsync(IdentityDbContext db, string sql) { DbConnection connection = db.Database.GetDbConnection(); await connection.OpenAsync(); await using DbCommand command = connection.CreateCommand(); command.CommandText = sql; long value = (long)(await command.ExecuteScalarAsync() ?? 0L); await connection.CloseAsync(); return value; }
}
