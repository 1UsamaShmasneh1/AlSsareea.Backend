using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Customers.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Customers.Application;

public static class CustomerPermissions
{
    public const string Read = "customers.customers.read";
    public const string Update = "customers.customers.update";
    public const string StatusManage = "customers.status.manage";
    public const string AddressesRead = "customers.addresses.read";
    public const string AddressesManage = "customers.addresses.manage";
    public const string HistoryRead = "customers.history.read";
}

public sealed class CustomersOptions
{
    public const string SectionName = "Customers";
    public int MaxAddressesPerCustomer { get; init; } = 20;
    public string DefaultLanguage { get; init; } = "ar";
    public string DefaultCurrency { get; init; } = "ILS";
    public int MaxDeliveryInstructionsLength { get; init; } = 1000;
    public bool AllowCustomerSelfProfileCreation { get; init; } = true;
}

public enum CustomerOperationStatus
{
    Success,
    Created,
    NotFound,
    Conflict,
    Invalid,
    Disabled,
}

public sealed record CustomerOperationResult<T>(CustomerOperationStatus Status, T? Value = default, string? ErrorCode = null);

public static class CustomerOperation
{
    public static CustomerOperationResult<T> Success<T>(T value) => new(CustomerOperationStatus.Success, value);
    public static CustomerOperationResult<T> Created<T>(T value) => new(CustomerOperationStatus.Created, value);
    public static CustomerOperationResult<T> Failure<T>(CustomerOperationStatus status, string code) => new(status, default, code);
}

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(CustomerId id, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<Customer?> GetByUserIdAsync(Guid userId, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
}

public interface ICustomersService
{
    Task<CustomerOperationResult<CustomerResponse>> CreateCurrentAsync(Guid userId, CreateCustomerRequest request, CancellationToken cancellationToken);
    Task<CustomerOperationResult<CustomerResponse>> GetCurrentAsync(Guid userId, CancellationToken cancellationToken);
    Task<CustomerOperationResult<CustomerResponse>> UpdateCurrentAsync(Guid userId, UpdateCustomerRequest request, CancellationToken cancellationToken);
    Task<CustomerOperationResult<IReadOnlyList<AddressResponse>>> GetCurrentAddressesAsync(Guid userId, CancellationToken cancellationToken);
    Task<CustomerOperationResult<AddressResponse>> GetCurrentAddressAsync(Guid userId, Guid addressId, CancellationToken cancellationToken);
    Task<CustomerOperationResult<AddressResponse>> AddCurrentAddressAsync(Guid userId, AddressRequest request, CancellationToken cancellationToken);
    Task<CustomerOperationResult<AddressResponse>> UpdateCurrentAddressAsync(Guid userId, Guid addressId, AddressRequest request, CancellationToken cancellationToken);
    Task<CustomerOperationResult<bool>> DeleteCurrentAddressAsync(Guid userId, Guid addressId, Guid concurrencyStamp, CancellationToken cancellationToken);
    Task<CustomerOperationResult<AddressResponse>> SetCurrentDefaultAddressAsync(Guid userId, Guid addressId, Guid concurrencyStamp, CancellationToken cancellationToken);
    Task<CustomerOperationResult<PreferencesResponse>> GetCurrentPreferencesAsync(Guid userId, CancellationToken cancellationToken);
    Task<CustomerOperationResult<PreferencesResponse>> UpdateCurrentPreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken);
    Task<CustomerOperationResult<CustomerListResponse>> GetCustomersAsync(int page, int pageSize, string? sort, short? status, string? search, CancellationToken cancellationToken);
    Task<CustomerOperationResult<CustomerAdminResponse>> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken);
    Task<CustomerOperationResult<CustomerAdminResponse>> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<CustomerOperationResult<CustomerAdminResponse>> ChangeStatusAsync(Guid customerId, ChangeCustomerStatusRequest request, Guid actorUserId, string correlationId, CancellationToken cancellationToken);
    Task<CustomerOperationResult<IReadOnlyList<StatusHistoryResponse>>> GetStatusHistoryAsync(Guid customerId, CancellationToken cancellationToken);
    Task<CustomerOperationResult<IReadOnlyList<AddressResponse>>> GetAddressesAsync(Guid customerId, CancellationToken cancellationToken);
}

public static class DependencyInjection
{
    public static IServiceCollection AddCustomersApplication(this IServiceCollection services) => services;
}
