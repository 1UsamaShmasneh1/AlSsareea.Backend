namespace AlSsareea.Modules.Maps.Contracts;

public sealed record ReverseGeocodingRequest(double Latitude, double Longitude);

public sealed record ReverseGeocodingResult(
    string FormattedAddress,
    double Latitude,
    double Longitude,
    string? PlaceId = null);

public interface IReverseGeocodingProvider
{
    Task<ReverseGeocodingResult?> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default);
}
