using System.Data;
using System.Data.Common;
using AlSsareea.Modules.Maps.Application;
using AlSsareea.Modules.Maps.Domain;
using AlSsareea.Modules.Maps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AlSsareea.IntegrationTests.Maps;

[Collection(MapsDatabaseFixtureSet.Name)]
public sealed class MapsPersistenceTests(MapsDatabaseFixture fixture)
{
    private static readonly DateTime CreatedAtUtc =
        new(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MigrationEnablesPostgisAndCreatesMapsSchema()
    {
        await using MapsDbContext context = fixture.CreateContext();

        object? extension = await ExecuteScalarAsync(
            context,
            "SELECT extname FROM pg_extension WHERE extname = 'postgis'");
        object? schema = await ExecuteScalarAsync(
            context,
            "SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'maps'");

        Assert.Equal("postgis", extension);
        Assert.Equal("maps", schema);
    }

    [Fact]
    public async Task BoundaryPersistsWithExpectedTypeAndSrid()
    {
        ServiceArea area = CreateArea();
        await PersistAsync(area);
        await using MapsDbContext context = fixture.CreateContext();
        var repository = new ServiceAreaRepository(context);

        ServiceArea? persisted = await repository.GetByIdAsync(area.Id, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal(area.Name, persisted.Name);
        Assert.Equal(4326, persisted.Boundary.SRID);
        Assert.IsType<MultiPolygon>(persisted.Boundary);
    }

    [Fact]
    public async Task SpatialQueriesDistinguishInsideOutsideAndBoundary()
    {
        ServiceArea area = CreateArea();
        await PersistAsync(area);
        await using MapsDbContext context = fixture.CreateContext();
        var repository = new ServiceAreaRepository(context);

        bool inside = await repository.ContainsPointAsync(
            area.Id,
            GeoPoint.Create(32, 35),
            CancellationToken.None);
        bool outside = await repository.ContainsPointAsync(
            area.Id,
            GeoPoint.Create(30, 35),
            CancellationToken.None);
        bool boundary = await repository.ContainsPointAsync(
            area.Id,
            GeoPoint.Create(31, 35),
            CancellationToken.None);
        IReadOnlyList<ServiceArea> containingAreas =
            await repository.FindContainingAreasAsync(
                GeoPoint.Create(32, 35),
                CancellationToken.None);

        Assert.True(inside);
        Assert.False(outside);
        Assert.True(boundary);
        Assert.Contains(containingAreas, candidate => candidate.Id == area.Id);
    }

    [Fact]
    public async Task BoundaryHasGistSpatialIndex()
    {
        await using MapsDbContext context = fixture.CreateContext();

        object? indexMethod = await ExecuteScalarAsync(
            context,
            """
            SELECT am.amname
            FROM pg_class index_class
            JOIN pg_am am ON am.oid = index_class.relam
            JOIN pg_namespace schema ON schema.oid = index_class.relnamespace
            WHERE schema.nspname = 'maps'
              AND index_class.relname = 'ix_service_areas_boundary'
            """);

        Assert.Equal("gist", indexMethod);
    }

    [Fact]
    public async Task ServiceAreasTableBelongsToMapsSchema()
    {
        await using MapsDbContext context = fixture.CreateContext();

        object? table = await ExecuteScalarAsync(
            context,
            """
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_schema = 'maps' AND table_name = 'service_areas'
            """);

        Assert.Equal("maps", table);
    }

    private async Task PersistAsync(ServiceArea area)
    {
        await using MapsDbContext context = fixture.CreateContext();
        var repository = new ServiceAreaRepository(context);
        await repository.AddAsync(area, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<object?> ExecuteScalarAsync(
        MapsDbContext context,
        string commandText)
    {
        DbConnection connection = context.Database.GetDbConnection();
        bool openedByHelper = connection.State == ConnectionState.Closed;

        if (openedByHelper)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = commandText;
            return await command.ExecuteScalarAsync();
        }
        finally
        {
            if (openedByHelper)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static ServiceArea CreateArea()
    {
        var shell = new LinearRing(
        [
            new Coordinate(34, 31),
            new Coordinate(36, 31),
            new Coordinate(36, 33),
            new Coordinate(34, 33),
            new Coordinate(34, 31),
        ]);
        var boundary = new MultiPolygon([new Polygon(shell)]) { SRID = 4326 };

        return ServiceArea.Create(
            ServiceAreaId.New(),
            $"Area {Guid.NewGuid():N}",
            "Integration test",
            boundary,
            CreatedAtUtc);
    }
}
