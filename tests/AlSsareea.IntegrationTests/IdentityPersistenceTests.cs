using System.Data.Common;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class IdentityPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task DatabaseCanConnectAndMigrationsAreApplied()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        Assert.True(await dbContext.Database.CanConnectAsync());
        Assert.Contains(
            await dbContext.Database.GetAppliedMigrationsAsync(),
            migration => migration.EndsWith("_InitializeIdentityPersistence", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("identity", "users", true)]
    [InlineData("identity", "__ef_migrations_history", true)]
    [InlineData("public", "users", false)]
    public async Task TableExistenceMatchesModuleBoundaries(
        string schema,
        string table,
        bool expected)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        bool exists = await ObjectExistsAsync(
            dbContext,
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @name)",
            schema,
            table);

        Assert.Equal(expected, exists);
    }

    [Fact]
    public async Task IdentitySchemaAndPostGisExtensionExist()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        bool schemaExists = await ScalarExistsAsync(
            dbContext,
            "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'identity')");
        bool postGisExists = await ScalarExistsAsync(
            dbContext,
            "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'postgis')");

        Assert.True(schemaExists);
        Assert.True(postGisExists);
    }

    [Fact]
    public async Task UserRoundTripsWithUtcTimestamp()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User user = User.Create(
            UserId.New(),
            "+970500000001",
            PreferredLanguage.Hebrew,
            new DateTime(2026, 7, 20, 21, 0, 0, DateTimeKind.Utc));

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        User persisted = await dbContext.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
        Assert.Equal(user.Id, persisted.Id);
        Assert.Equal(user.PhoneNumber, persisted.PhoneNumber);
        Assert.Equal(user.PreferredLanguage, persisted.PreferredLanguage);
        Assert.Equal(user.Status, persisted.Status);
        Assert.Equal(user.CreatedAtUtc, persisted.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, persisted.CreatedAtUtc.Kind);
    }

    private static async Task<bool> ObjectExistsAsync(
        IdentityDbContext dbContext,
        string sql,
        string schema,
        string name)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        DbParameter schemaParameter = command.CreateParameter();
        schemaParameter.ParameterName = "schema";
        schemaParameter.Value = schema;
        command.Parameters.Add(schemaParameter);
        DbParameter nameParameter = command.CreateParameter();
        nameParameter.ParameterName = "name";
        nameParameter.Value = name;
        command.Parameters.Add(nameParameter);

        bool exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        await connection.CloseAsync();
        return exists;
    }

    private static async Task<bool> ScalarExistsAsync(IdentityDbContext dbContext, string sql)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        bool exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        await connection.CloseAsync();
        return exists;
    }
}
