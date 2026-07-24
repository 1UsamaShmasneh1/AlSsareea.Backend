using AlSsareea.Modules.Maps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AlSsareea.IntegrationTests.Maps;

public sealed class MapsDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgis/postgis:17-3.5")
        .WithDatabase("alssareea_maps_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    internal DbContextOptions<MapsDbContext> DbContextOptions { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        DbContextOptions = new DbContextOptionsBuilder<MapsDbContext>()
            .UseNpgsql(
                _database.GetConnectionString(),
                npgsqlOptions => npgsqlOptions.UseNetTopologySuite())
            .Options;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    internal MapsDbContext CreateContext() => new(DbContextOptions);

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MapsDatabaseFixtureSet : ICollectionFixture<MapsDatabaseFixture>
{
    public const string Name = "Maps database";
}
