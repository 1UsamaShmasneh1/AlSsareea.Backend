using AlSsareea.BuildingBlocks.Domain;
using NetTopologySuite.Geometries;

namespace AlSsareea.Modules.Maps.Domain;

public readonly record struct GeoPoint
{
    public const int SpatialReferenceId = 4326;

    private GeoPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static GeoPoint Create(double latitude, double longitude)
    {
        ValidateFinite(latitude, nameof(latitude));
        ValidateFinite(longitude, nameof(longitude));

        if (latitude is < -90 or > 90)
        {
            throw new DomainException("Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new DomainException("Longitude must be between -180 and 180.");
        }

        return new GeoPoint(latitude, longitude);
    }

    public Point ToPoint() => new(Longitude, Latitude) { SRID = SpatialReferenceId };

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new DomainException($"{parameterName} must be a finite number.");
        }
    }
}
