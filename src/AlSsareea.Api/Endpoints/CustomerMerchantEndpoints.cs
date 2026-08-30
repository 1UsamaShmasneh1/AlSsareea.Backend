using AlSsareea.Modules.Merchants.Application;
using AlSsareea.Modules.Merchants.Contracts;

namespace AlSsareea.Api.Endpoints;

internal static class CustomerMerchantEndpoints
{
    public static IEndpointRouteBuilder MapCustomerMerchantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/customer/merchants").WithTags("Customer Merchants").AllowAnonymous();
        group.MapGet("/", Discover).RequireRateLimiting("merchants-read").WithName("DiscoverCustomerMerchants")
            .Produces<CustomerMerchantListResponse>().ProducesProblem(400);
        group.MapGet("/{merchantId:guid}", Details).RequireRateLimiting("merchants-read").WithName("GetCustomerMerchantDetails")
            .Produces<CustomerMerchantDetails>().ProducesProblem(404);
        return endpoints;
    }

    private static Task<IResult> Discover(int? page, int? pageSize, string? query, bool? openNow, ICustomerMerchantQueryService service, CancellationToken ct) =>
        Result(service.DiscoverAsync(page ?? 1, pageSize ?? 20, query, openNow, ct));

    private static Task<IResult> Details(Guid merchantId, ICustomerMerchantQueryService service, CancellationToken ct) =>
        Result(service.GetDetailsAsync(merchantId, ct));

    private static async Task<IResult> Result<T>(Task<MerchantOperationResult<T>> operation)
    {
        MerchantOperationResult<T> result = await operation;
        return result.Status switch
        {
            MerchantOperationStatus.Success => Results.Ok(result.Value),
            MerchantOperationStatus.NotFound => Problem(404, result.ErrorCode),
            _ => Problem(400, result.ErrorCode),
        };
    }

    private static IResult Problem(int status, string? code) => Results.Problem(
        statusCode: status,
        title: status == 404 ? "Not found" : "Invalid request",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
