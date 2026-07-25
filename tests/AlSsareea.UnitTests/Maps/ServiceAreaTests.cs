using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Maps.Domain;
using NetTopologySuite.Geometries;

namespace AlSsareea.UnitTests.Maps;

public sealed class ServiceAreaTests
{
    private static readonly DateTime CreatedAtUtc =
        new(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DeactivateMarksAreaInactive()
    {
        ServiceArea area = CreateArea();
        DateTime updatedAtUtc = CreatedAtUtc.AddMinutes(1);

        area.Deactivate(updatedAtUtc);

        Assert.False(area.IsActive);
        Assert.Equal(updatedAtUtc, area.UpdatedAtUtc);
    }

    [Fact]
    public void ActivateMarksAreaActive()
    {
        ServiceArea area = CreateArea();
        area.Deactivate(CreatedAtUtc.AddMinutes(1));
        DateTime updatedAtUtc = CreatedAtUtc.AddMinutes(2);

        area.Activate(updatedAtUtc);

        Assert.True(area.IsActive);
        Assert.Equal(updatedAtUtc, area.UpdatedAtUtc);
    }

    [Fact]
    public void CreateRejectsEmptyBoundary()
    {
        var emptyBoundary = new MultiPolygon([]) { SRID = 4326 };

        Assert.Throws<DomainException>(() => ServiceArea.Create(
            ServiceAreaId.New(),
            "Empty",
            null,
            emptyBoundary,
            CreatedAtUtc));
    }

    [Fact]
    public void CreateRejectsInvalidBoundarySrid()
    {
        MultiPolygon boundary = CreateBoundary();
        boundary.SRID = 3857;

        Assert.Throws<DomainException>(() => ServiceArea.Create(
            ServiceAreaId.New(),
            "Wrong SRID",
            null,
            boundary,
            CreatedAtUtc));
    }

    [Fact]
    public void CreateRejectsSelfIntersectingBoundary()
    {
        var shell = new LinearRing(
        [
            new Coordinate(34, 31),
            new Coordinate(36, 33),
            new Coordinate(36, 31),
            new Coordinate(34, 33),
            new Coordinate(34, 31),
        ]);
        var boundary = new MultiPolygon([new Polygon(shell)]) { SRID = 4326 };

        Assert.Throws<DomainException>(() => ServiceArea.Create(
            ServiceAreaId.New(),
            "Invalid",
            null,
            boundary,
            CreatedAtUtc));
    }

    [Fact]
    public void ContainsIncludesPointOnBoundary()
    {
        ServiceArea area = CreateArea();

        bool contains = area.Contains(GeoPoint.Create(31, 35));

        Assert.True(contains);
    }

    internal static MultiPolygon CreateBoundary()
    {
        var shell = new LinearRing(
        [
            new Coordinate(34, 31),
            new Coordinate(36, 31),
            new Coordinate(36, 33),
            new Coordinate(34, 33),
            new Coordinate(34, 31),
        ]);

        return new MultiPolygon([new Polygon(shell)]) { SRID = 4326 };
    }

    private static ServiceArea CreateArea() =>
        ServiceArea.Create(
            ServiceAreaId.New(),
            "Central",
            "Test area",
            CreateBoundary(),
            CreatedAtUtc);
}
