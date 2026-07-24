using AlSsareea.Api.Security;
using AlSsareea.Modules.Customers.Application;
using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Identity.Application;
using Microsoft.AspNetCore.Mvc;

namespace AlSsareea.Api.Endpoints;

internal static class CustomerEndpoints
{
    internal static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder current = endpoints.MapGroup("/api/v1/customers/me").WithTags("Customers").RequireAuthorization();
        current.MapPost("/", CreateAsync).RequireRateLimiting("customers-self-write").WithName("CreateCurrentCustomer").Produces<CustomerResponse>(201).ProducesProblem(400).ProducesProblem(409);
        current.MapGet("/", GetAsync).WithName("GetCurrentCustomer").Produces<CustomerResponse>().ProducesProblem(404);
        current.MapPut("/", UpdateAsync).RequireRateLimiting("customers-self-write").WithName("UpdateCurrentCustomer").Produces<CustomerResponse>().ProducesProblem(409);
        current.MapGet("/addresses", GetAddressesAsync).WithName("GetCurrentCustomerAddresses");
        current.MapGet("/addresses/{addressId:guid}", GetAddressAsync).WithName("GetCurrentCustomerAddress").ProducesProblem(404);
        current.MapPost("/addresses", AddAddressAsync).RequireRateLimiting("customers-address-write").WithName("AddCurrentCustomerAddress").Produces<AddressResponse>(201);
        current.MapPut("/addresses/{addressId:guid}", UpdateAddressAsync).RequireRateLimiting("customers-address-write").WithName("UpdateCurrentCustomerAddress");
        current.MapDelete("/addresses/{addressId:guid}", DeleteAddressAsync).RequireRateLimiting("customers-address-write").WithName("DeleteCurrentCustomerAddress");
        current.MapPut("/addresses/{addressId:guid}/default", SetDefaultAddressAsync).RequireRateLimiting("customers-address-write").WithName("SetCurrentCustomerDefaultAddress");
        current.MapGet("/preferences", GetPreferencesAsync).WithName("GetCurrentCustomerPreferences");
        current.MapPut("/preferences", UpdatePreferencesAsync).RequireRateLimiting("customers-self-write").WithName("UpdateCurrentCustomerPreferences");

        RouteGroupBuilder admin = endpoints.MapGroup("/api/v1/admin/customers").WithTags("Customer Administration").RequireAuthorization();
        admin.MapGet("/", GetCustomersAsync).RequireAuthorization(Permission(CustomerPermissions.Read)).RequireRateLimiting("customers-admin-read").WithName("GetCustomers");
        admin.MapGet("/{customerId:guid}", GetCustomerAsync).RequireAuthorization(Permission(CustomerPermissions.Read)).RequireRateLimiting("customers-admin-read").WithName("GetCustomerById");
        admin.MapPut("/{customerId:guid}", UpdateCustomerByAdminAsync).RequireAuthorization(Permission(CustomerPermissions.Update)).RequireRateLimiting("customers-admin-write").WithName("UpdateCustomerByAdmin");
        admin.MapPut("/{customerId:guid}/status", ChangeStatusAsync).RequireAuthorization(Permission(CustomerPermissions.StatusManage)).RequireRateLimiting("customers-admin-write").WithName("ChangeCustomerStatus");
        admin.MapGet("/{customerId:guid}/status-history", GetHistoryAsync).RequireAuthorization(Permission(CustomerPermissions.HistoryRead)).RequireRateLimiting("customers-admin-read").WithName("GetCustomerStatusHistory");
        admin.MapGet("/{customerId:guid}/addresses", GetAddressesByAdminAsync).RequireAuthorization(Permission(CustomerPermissions.AddressesRead)).RequireRateLimiting("customers-admin-read").WithName("GetCustomerAddressesByAdmin");
        return endpoints;
    }

    private static Task<IResult> CreateAsync(CreateCustomerRequest request, ICurrentUser current, ICustomersService service, CancellationToken ct)
        => Run(service.CreateCurrentAsync(User(current), request, ct), "/api/v1/customers/me");
    private static Task<IResult> GetAsync(ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.GetCurrentAsync(User(current), ct));
    private static Task<IResult> UpdateAsync(UpdateCustomerRequest request, ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.UpdateCurrentAsync(User(current), request, ct));
    private static Task<IResult> GetAddressesAsync(ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.GetCurrentAddressesAsync(User(current), ct));
    private static Task<IResult> GetAddressAsync(Guid addressId, ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.GetCurrentAddressAsync(User(current), addressId, ct));
    private static Task<IResult> AddAddressAsync(AddressRequest request, ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.AddCurrentAddressAsync(User(current), request, ct), $"/api/v1/customers/me/addresses");
    private static Task<IResult> UpdateAddressAsync(Guid addressId, AddressRequest request, ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.UpdateCurrentAddressAsync(User(current), addressId, request, ct));
    private static Task<IResult> DeleteAddressAsync(Guid addressId, [FromQuery] Guid concurrencyStamp, ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.DeleteCurrentAddressAsync(User(current), addressId, concurrencyStamp, ct), noContent: true);
    private static Task<IResult> SetDefaultAddressAsync(Guid addressId, ConcurrencyRequest request, ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.SetCurrentDefaultAddressAsync(User(current), addressId, request.ConcurrencyStamp, ct));
    private static Task<IResult> GetPreferencesAsync(ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.GetCurrentPreferencesAsync(User(current), ct));
    private static Task<IResult> UpdatePreferencesAsync(UpdatePreferencesRequest request, ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.UpdateCurrentPreferencesAsync(User(current), request, ct));
    private static Task<IResult> GetCustomersAsync(int page, int pageSize, string? sort, short? status, string? search, ICustomersService service, CancellationToken ct) => Run(service.GetCustomersAsync(page, pageSize, sort, status, search, ct));
    private static Task<IResult> GetCustomerAsync(Guid customerId, ICustomersService service, CancellationToken ct) => Run(service.GetCustomerAsync(customerId, ct));
    private static Task<IResult> UpdateCustomerByAdminAsync(Guid customerId, UpdateCustomerRequest request, ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.UpdateCustomerAsync(customerId, request, User(current), ct));
    private static Task<IResult> ChangeStatusAsync(Guid customerId, ChangeCustomerStatusRequest request, HttpContext context, ICurrentUser current, ICustomersService service, CancellationToken ct) => Run(service.ChangeStatusAsync(customerId, request, User(current), context.TraceIdentifier, ct));
    private static Task<IResult> GetHistoryAsync(Guid customerId, ICustomersService service, CancellationToken ct) => Run(service.GetStatusHistoryAsync(customerId, ct));
    private static Task<IResult> GetAddressesByAdminAsync(Guid customerId, ICustomersService service, CancellationToken ct) => Run(service.GetAddressesAsync(customerId, ct));

    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;
    private static Guid User(ICurrentUser current) => current.UserId?.Value ?? Guid.Empty;

    private static async Task<IResult> Run<T>(Task<CustomerOperationResult<T>> operation, string? location = null, bool noContent = false)
    {
        CustomerOperationResult<T> result = await operation;
        return result.Status switch
        {
            CustomerOperationStatus.Success when noContent => Results.NoContent(),
            CustomerOperationStatus.Success => Results.Ok(result.Value),
            CustomerOperationStatus.Created => Results.Created(location, result.Value),
            CustomerOperationStatus.NotFound => Problem(404, result.ErrorCode),
            CustomerOperationStatus.Conflict => Problem(409, result.ErrorCode),
            CustomerOperationStatus.Disabled => Problem(403, result.ErrorCode),
            _ => Problem(400, result.ErrorCode),
        };
    }

    private static IResult Problem(int status, string? code) => Results.Problem(
        statusCode: status,
        title: status switch { 400 => "Invalid request", 403 => "Forbidden", 404 => "Not found", 409 => "Conflict", _ => "Request failed" },
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
