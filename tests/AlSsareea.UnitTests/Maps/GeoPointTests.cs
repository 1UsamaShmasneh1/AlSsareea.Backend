using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Maps.Domain;

namespace AlSsareea.UnitTests.Maps;

public sealed class GeoPointTests
{
    [Theory]
    [InlineData(-90)]
    [InlineData(0)]
    [InlineData(90)]
    public void CreateAcceptsValidLatitude(double latitude)
    {
        GeoPoint point = GeoPoint.Create(latitude, 35);

        Assert.Equal(latitude, point.Latitude);
    }

    [Theory]
    [InlineData(-90.000001)]
    [InlineData(90.000001)]
    public void CreateRejectsInvalidLatitude(double latitude)
    {
        Assert.Throws<DomainException>(() => GeoPoint.Create(latitude, 35));
    }

    [Theory]
    [InlineData(-180)]
    [InlineData(0)]
    [InlineData(180)]
    public void CreateAcceptsValidLongitude(double longitude)
    {
        GeoPoint point = GeoPoint.Create(32, longitude);

        Assert.Equal(longitude, point.Longitude);
    }

    [Theory]
    [InlineData(-180.000001)]
    [InlineData(180.000001)]
    public void CreateRejectsInvalidLongitude(double longitude)
    {
        Assert.Throws<DomainException>(() => GeoPoint.Create(32, longitude));
    }

    [Theory]
    [InlineData(double.NaN, 35)]
    [InlineData(32, double.NaN)]
    [InlineData(double.PositiveInfinity, 35)]
    [InlineData(double.NegativeInfinity, 35)]
    [InlineData(32, double.PositiveInfinity)]
    [InlineData(32, double.NegativeInfinity)]
    public void CreateRejectsNonFiniteCoordinates(double latitude, double longitude)
    {
        Assert.Throws<DomainException>(() => GeoPoint.Create(latitude, longitude));
    }

    [Fact]
    public void ToPointUsesLongitudeForXLatitudeForYAndSrid4326()
    {
        GeoPoint geoPoint = GeoPoint.Create(32.0853, 34.7818);

        var point = geoPoint.ToPoint();

        Assert.Equal(34.7818, point.X);
        Assert.Equal(32.0853, point.Y);
        Assert.Equal(4326, point.SRID);
    }
}
