using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Customers.Domain;

public readonly record struct GeoCoordinate
{
    public GeoCoordinate(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90) throw new DomainException("Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180) throw new DomainException("Longitude must be between -180 and 180.");
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }
    public double Longitude { get; }
}
