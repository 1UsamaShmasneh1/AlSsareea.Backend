using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Maps.Domain;
using AlSsareea.Modules.Maps.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Maps.Infrastructure.Providers;

public sealed class FakeMapsProvider(IOptions<MapsOptions> options) : IMapsProvider
{
    private const double EarthRadiusMeters = 6_371_000;
    private const double AssumedSpeedMetersPerSecond = 40_000d / 3_600d;

    public Task<IReadOnlyList<GeocodingResult>> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfFailureConfigured();
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Task.FromResult<IReadOnlyList<GeocodingResult>>([]);
        }

        string normalizedQuery = request.Query.Trim();
        Coordinates coordinates = CoordinatesFrom(normalizedQuery);
        string placeId = CreatePlaceId(normalizedQuery);
        IReadOnlyList<GeocodingResult> results =
        [
            new(
                FormatAddress(normalizedQuery),
                coordinates.Latitude,
                coordinates.Longitude,
                placeId),
        ];

        return Task.FromResult(results);
    }

    public Task<ReverseGeocodingResult?> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfFailureConfigured();
        ArgumentNullException.ThrowIfNull(request);
        _ = GeoPoint.Create(request.Latitude, request.Longitude);

        string coordinateKey = FormattableString.Invariant(
            $"{request.Latitude:F6},{request.Longitude:F6}");
        ReverseGeocodingResult result = new(
            $"Fake address at {coordinateKey}",
            request.Latitude,
            request.Longitude,
            CreatePlaceId(coordinateKey));

        return Task.FromResult<ReverseGeocodingResult?>(result);
    }

    public Task<IReadOnlyList<PlaceSuggestion>> AutocompleteAsync(
        PlaceAutocompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfFailureConfigured();
        ArgumentNullException.ThrowIfNull(request);
        ValidateOptionalLocation(request.Latitude, request.Longitude);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Task.FromResult<IReadOnlyList<PlaceSuggestion>>([]);
        }

        string query = request.Query.Trim();
        IReadOnlyList<PlaceSuggestion> suggestions =
        [
            new(CreatePlaceId($"{query}:1"), $"{query} Central", "Fake district"),
            new(CreatePlaceId($"{query}:2"), $"{query} North", "Fake district"),
            new(CreatePlaceId($"{query}:3"), $"{query} South", "Fake district"),
        ];

        return Task.FromResult(suggestions);
    }

    public Task<PlaceDetails?> GetPlaceDetailsAsync(
        string placeId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfFailureConfigured();

        if (string.IsNullOrWhiteSpace(placeId))
        {
            return Task.FromResult<PlaceDetails?>(null);
        }

        string normalizedPlaceId = placeId.Trim();
        Coordinates coordinates = CoordinatesFrom(normalizedPlaceId);
        PlaceDetails details = new(
            normalizedPlaceId,
            $"Fake place {normalizedPlaceId[^Math.Min(8, normalizedPlaceId.Length)..]}",
            $"Fake address for {normalizedPlaceId}",
            coordinates.Latitude,
            coordinates.Longitude);

        return Task.FromResult<PlaceDetails?>(details);
    }

    public Task<RouteResult> CalculateRouteAsync(
        RouteRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfFailureConfigured();
        ArgumentNullException.ThrowIfNull(request);

        Coordinates[] path =
        [
            request.Origin,
            .. request.Waypoints ?? [],
            request.Destination,
        ];

        foreach (Coordinates coordinate in path)
        {
            _ = GeoPoint.Create(coordinate.Latitude, coordinate.Longitude);
        }

        double distance = 0;
        for (int index = 1; index < path.Length; index++)
        {
            distance += HaversineDistance(path[index - 1], path[index]);
        }

        long distanceMeters = checked((long)Math.Round(distance, MidpointRounding.AwayFromZero));
        var duration = TimeSpan.FromSeconds(distance / AssumedSpeedMetersPerSecond);
        return Task.FromResult<RouteResult>(new(distanceMeters, duration, path));
    }

    private static Coordinates CoordinatesFrom(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToUpperInvariant()));
        ulong latitudeBits = BinaryPrimitives.ReadUInt64BigEndian(hash);
        ulong longitudeBits = BinaryPrimitives.ReadUInt64BigEndian(hash.AsSpan(sizeof(ulong)));
        double latitude = ((latitudeBits / (double)ulong.MaxValue) * 170) - 85;
        double longitude = ((longitudeBits / (double)ulong.MaxValue) * 360) - 180;
        return new Coordinates(latitude, longitude);
    }

    private static string CreatePlaceId(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToUpperInvariant()));
        return $"fake-{Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant()}";
    }

    private static string FormatAddress(string query) => $"Fake address for {query}";

    private static void ValidateOptionalLocation(double? latitude, double? longitude)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new ArgumentException("Latitude and longitude must be supplied together.");
        }

        if (latitude.HasValue)
        {
            _ = GeoPoint.Create(latitude.Value, longitude!.Value);
        }
    }

    private static double HaversineDistance(Coordinates origin, Coordinates destination)
    {
        double latitude1 = DegreesToRadians(origin.Latitude);
        double latitude2 = DegreesToRadians(destination.Latitude);
        double latitudeDelta = DegreesToRadians(destination.Latitude - origin.Latitude);
        double longitudeDelta = DegreesToRadians(destination.Longitude - origin.Longitude);

        double haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + (Math.Cos(latitude1)
                * Math.Cos(latitude2)
                * Math.Pow(Math.Sin(longitudeDelta / 2), 2));

        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private void ThrowIfFailureConfigured()
    {
        if (options.Value.Fake.FailAllRequests)
        {
            throw new MapsProviderException("The fake maps provider is configured to fail.");
        }
    }
}
