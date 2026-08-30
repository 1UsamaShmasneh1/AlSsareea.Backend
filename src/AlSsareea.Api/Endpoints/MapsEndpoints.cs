using AlSsareea.Modules.Maps.Application;
using AlSsareea.Modules.Maps.Contracts;

namespace AlSsareea.Api.Endpoints;

internal static class MapsEndpoints
{
    public static IEndpointRouteBuilder MapCustomerMapsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/maps").WithTags("Customer Maps").RequireAuthorization().RequireRateLimiting("maps-read");
        group.MapPost("/geocode", Geocode).WithName("GeocodeCustomerAddress").Produces<IReadOnlyList<GeocodingResult>>().ProducesProblem(400).ProducesProblem(503);
        group.MapPost("/reverse-geocode", ReverseGeocode).WithName("ReverseGeocodeCustomerLocation").Produces<ReverseGeocodingResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(503);
        group.MapPost("/delivery-eligibility", DeliveryEligibility).WithName("EvaluateCustomerDeliveryEligibility").Produces<DeliveryEligibilityResponse>().ProducesProblem(400);
        return endpoints;
    }

    private static Task<IResult> Geocode(GeocodingRequest request, ICustomerMapsService service, CancellationToken ct) => Result(service.GeocodeAsync(request, ct));
    private static Task<IResult> ReverseGeocode(ReverseGeocodingRequest request, ICustomerMapsService service, CancellationToken ct) => Result(service.ReverseGeocodeAsync(request, ct));
    private static Task<IResult> DeliveryEligibility(DeliveryEligibilityRequest request, ICustomerMapsService service, CancellationToken ct) => Result(service.EvaluateDeliveryEligibilityAsync(request, ct));

    private static async Task<IResult> Result<T>(Task<CustomerMapsResult<T>> operation)
    {
        CustomerMapsResult<T> result = await operation;
        return result.Status switch
        {
            CustomerMapsStatus.Success => Results.Ok(result.Value),
            CustomerMapsStatus.NotFound => Problem(404, result.ErrorCode),
            CustomerMapsStatus.Unavailable => Problem(503, result.ErrorCode),
            _ => Problem(400, result.ErrorCode),
        };
    }

    private static IResult Problem(int status, string? code) => Results.Problem(
        statusCode: status,
        title: status switch { 404 => "Not found", 503 => "Service unavailable", _ => "Invalid request" },
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
