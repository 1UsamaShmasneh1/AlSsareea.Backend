namespace AlSsareea.Modules.Customers.Contracts;

public sealed record CustomerNotificationRecipient(Guid UserId, string Language);
public interface ICustomerNotificationRecipientProvider { Task<CustomerNotificationRecipient?> GetAsync(Guid customerId, CancellationToken cancellationToken = default); }

public sealed record CartCustomerContext(Guid CustomerId, bool IsAllowed);
public interface ICartCustomerContextProvider
{
    Task<CartCustomerContext?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
public sealed record OrderAddressSnapshotContract(Guid AddressId, string Label, string City, string? Area, string Street, string? BuildingNumber, string? Floor, string? Apartment, string? DeliveryInstructions, double? Latitude, double? Longitude, string? PlaceId, string FormattedAddress);
public sealed record OrderCustomerSnapshotContract(Guid CustomerId, string DisplayName, string? PhoneNumber, string PreferredLanguage, OrderAddressSnapshotContract Address);
public interface IOrderCustomerSnapshotProvider
{
    Task<Guid?> GetCustomerIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrderCustomerSnapshotContract?> GetAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default);
}

public interface ICustomerIdentityProvider
{
    Task<Guid?> GetUserIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}

public sealed record CreateCustomerRequest(string FirstName, string LastName, DateOnly? DateOfBirth);
public sealed record UpdateCustomerRequest(string FirstName, string LastName, DateOnly? DateOfBirth, Guid ConcurrencyStamp);
public sealed record ChangeCustomerStatusRequest(short Status, string? Reason, Guid ConcurrencyStamp);
public sealed record ConcurrencyRequest(Guid ConcurrencyStamp);

public sealed record CustomerResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    DateOnly? DateOfBirth,
    short Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid ConcurrencyStamp);

public sealed record CustomerAdminResponse(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string DisplayName,
    DateOnly? DateOfBirth,
    short Status,
    string? BlockReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? DeletedAtUtc,
    Guid ConcurrencyStamp);

public sealed record CustomerListResponse(IReadOnlyList<CustomerAdminResponse> Items, int Page, int PageSize, int TotalCount);

public sealed record AddressRequest(
    string Label,
    short AddressType,
    string City,
    string? Area,
    string Street,
    string? BuildingNumber,
    string? Floor,
    string? Apartment,
    string? PostalCode,
    string? PlaceId,
    double? Latitude,
    double? Longitude,
    string? DeliveryInstructions,
    bool IsDefault,
    Guid? ConcurrencyStamp);

public sealed record AddressResponse(
    Guid Id,
    string Label,
    short AddressType,
    string City,
    string? Area,
    string Street,
    string? BuildingNumber,
    string? Floor,
    string? Apartment,
    string? PostalCode,
    string? PlaceId,
    double? Latitude,
    double? Longitude,
    string? DeliveryInstructions,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid ConcurrencyStamp);

public sealed record UpdatePreferencesRequest(
    string PreferredLanguage,
    string PreferredCurrency,
    bool AllowMarketingNotifications,
    bool AllowOrderStatusNotifications,
    bool AllowPromotionalNotifications,
    Guid ConcurrencyStamp);

public sealed record PreferencesResponse(
    string PreferredLanguage,
    string PreferredCurrency,
    bool AllowMarketingNotifications,
    bool AllowOrderStatusNotifications,
    bool AllowPromotionalNotifications,
    DateTime UpdatedAtUtc,
    Guid ConcurrencyStamp);

public sealed record StatusHistoryResponse(
    Guid Id,
    short PreviousStatus,
    short NewStatus,
    string? Reason,
    DateTime ChangedAtUtc,
    Guid ChangedByUserId,
    string CorrelationId);
