namespace AlSsareea.Modules.Maps.Contracts;

public sealed record GeocodingRequest(string Query, string? CountryCode = null);

public sealed record GeocodingResult(
    string FormattedAddress,
    double Latitude,
    double Longitude,
    string? PlaceId = null);

public interface IGeocodingProvider
{
    Task<IReadOnlyList<GeocodingResult>> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default);
}
