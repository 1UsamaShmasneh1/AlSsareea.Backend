using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Maps.Contracts;

namespace AlSsareea.Modules.Maps.Application;

public enum CustomerMapsStatus { Success, Invalid, NotFound, Unavailable }
public sealed record CustomerMapsResult<T>(CustomerMapsStatus Status, T? Value = default, string? ErrorCode = null);
public sealed record DeliveryEligibilityRequest(double Latitude, double Longitude);
public sealed record DeliveryEligibilityResponse(bool Eligible, Guid? ServiceAreaId, string? ReasonCode);

public interface ICustomerMapsService
{
    Task<CustomerMapsResult<IReadOnlyList<GeocodingResult>>> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken);
    Task<CustomerMapsResult<ReverseGeocodingResult>> ReverseGeocodeAsync(ReverseGeocodingRequest request, CancellationToken cancellationToken);
    Task<CustomerMapsResult<DeliveryEligibilityResponse>> EvaluateDeliveryEligibilityAsync(DeliveryEligibilityRequest request, CancellationToken cancellationToken);
}

public sealed class CustomerMapsService(IGeocodingProvider geocoding, IReverseGeocodingProvider reverseGeocoding, IMapsModule maps) : ICustomerMapsService
{
    public async Task<CustomerMapsResult<IReadOnlyList<GeocodingResult>>> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Query) || request.Query.Trim().Length > 500) return Invalid<IReadOnlyList<GeocodingResult>>();
        try { return Success(await geocoding.GeocodeAsync(new(request.Query.Trim(), request.CountryCode?.Trim()), cancellationToken)); }
        catch (MapsProviderException) { return Unavailable<IReadOnlyList<GeocodingResult>>(); }
    }

    public async Task<CustomerMapsResult<ReverseGeocodingResult>> ReverseGeocodeAsync(ReverseGeocodingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ReverseGeocodingResult? result = await reverseGeocoding.ReverseGeocodeAsync(request, cancellationToken);
            return result is null ? new(CustomerMapsStatus.NotFound, ErrorCode: "maps.location_not_found") : Success(result);
        }
        catch (Exception exception) when (exception is ArgumentException or DomainException) { return Invalid<ReverseGeocodingResult>(); }
        catch (MapsProviderException) { return Unavailable<ReverseGeocodingResult>(); }
    }

    public async Task<CustomerMapsResult<DeliveryEligibilityResponse>> EvaluateDeliveryEligibilityAsync(DeliveryEligibilityRequest request, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<ServiceAreaDetails> areas = await maps.FindContainingAreasAsync(request.Latitude, request.Longitude, cancellationToken);
            ServiceAreaDetails? active = areas.Where(x => x.IsActive).OrderBy(x => x.Name).ThenBy(x => x.Id).FirstOrDefault();
            DeliveryEligibilityResponse response = active is null ? new(false, null, "maps.outside_service_area") : new(true, active.Id, null);
            return Success(response);
        }
        catch (Exception exception) when (exception is ArgumentException or DomainException) { return Invalid<DeliveryEligibilityResponse>(); }
    }

    private static CustomerMapsResult<T> Success<T>(T value) => new(CustomerMapsStatus.Success, value);
    private static CustomerMapsResult<T> Invalid<T>() => new(CustomerMapsStatus.Invalid, ErrorCode: "maps.invalid_request");
    private static CustomerMapsResult<T> Unavailable<T>() => new(CustomerMapsStatus.Unavailable, ErrorCode: "maps.provider_unavailable");
}
