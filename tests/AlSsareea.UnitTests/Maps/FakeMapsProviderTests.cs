using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Maps.Infrastructure.Configuration;
using AlSsareea.Modules.Maps.Infrastructure.Providers;
using Microsoft.Extensions.Options;

namespace AlSsareea.UnitTests.Maps;

public sealed class FakeMapsProviderTests
{
    [Fact]
    public async Task GeocodingIsDeterministic()
    {
        FakeMapsProvider provider = CreateProvider();
        var request = new GeocodingRequest("Tel Aviv");

        IReadOnlyList<GeocodingResult> first =
            await provider.GeocodeAsync(request, CancellationToken.None);
        IReadOnlyList<GeocodingResult> second =
            await provider.GeocodeAsync(request, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Single(first);
    }

    [Fact]
    public async Task ReverseGeocodingPreservesCoordinateOrder()
    {
        FakeMapsProvider provider = CreateProvider();

        ReverseGeocodingResult? result = await provider.ReverseGeocodeAsync(
            new ReverseGeocodingRequest(32.0853, 34.7818),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(32.0853, result.Latitude);
        Assert.Equal(34.7818, result.Longitude);
    }

    [Fact]
    public async Task AutocompleteAndPlaceDetailsReturnPredictableData()
    {
        FakeMapsProvider provider = CreateProvider();

        IReadOnlyList<PlaceSuggestion> suggestions = await provider.AutocompleteAsync(
            new PlaceAutocompleteRequest("Market"),
            CancellationToken.None);
        PlaceDetails? details = await provider.GetPlaceDetailsAsync(
            suggestions[0].PlaceId,
            CancellationToken.None);

        Assert.Equal(3, suggestions.Count);
        Assert.NotNull(details);
        Assert.Equal(suggestions[0].PlaceId, details.PlaceId);
    }

    [Fact]
    public async Task RouteUsesHaversineApproximation()
    {
        FakeMapsProvider provider = CreateProvider();

        RouteResult result = await provider.CalculateRouteAsync(
            new RouteRequest(new Coordinates(0, 0), new Coordinates(0, 1)),
            CancellationToken.None);

        Assert.InRange(result.DistanceMeters, 111_000, 112_000);
        Assert.True(result.EstimatedDuration > TimeSpan.Zero);
        Assert.Equal(2, result.Path.Count);
    }

    [Fact]
    public async Task CancellationIsObserved()
    {
        FakeMapsProvider provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.GeocodeAsync(new GeocodingRequest("Cancelled"), cancellation.Token));
    }

    [Fact]
    public async Task ConfiguredFailureThrowsProviderNeutralException()
    {
        FakeMapsProvider provider = CreateProvider(failAllRequests: true);

        await Assert.ThrowsAsync<MapsProviderException>(() =>
            provider.GeocodeAsync(new GeocodingRequest("Failure"), CancellationToken.None));
    }

    private static FakeMapsProvider CreateProvider(bool failAllRequests = false)
    {
        var options = Options.Create(new MapsOptions
        {
            Provider = MapsProvider.Fake,
            Fake = new FakeMapsOptions { FailAllRequests = failAllRequests },
        });

        return new FakeMapsProvider(options);
    }
}
