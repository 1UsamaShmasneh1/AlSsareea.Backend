namespace AlSsareea.Modules.Maps.Contracts;

public sealed record PlaceAutocompleteRequest(
    string Query,
    double? Latitude = null,
    double? Longitude = null,
    int? RadiusMeters = null);

public sealed record PlaceSuggestion(
    string PlaceId,
    string PrimaryText,
    string SecondaryText);

public sealed record PlaceDetails(
    string PlaceId,
    string Name,
    string FormattedAddress,
    double Latitude,
    double Longitude);

public interface IPlacesProvider
{
    Task<IReadOnlyList<PlaceSuggestion>> AutocompleteAsync(
        PlaceAutocompleteRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaceDetails?> GetPlaceDetailsAsync(
        string placeId,
        CancellationToken cancellationToken = default);
}
