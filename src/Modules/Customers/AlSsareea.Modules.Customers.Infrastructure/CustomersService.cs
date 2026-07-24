using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Customers.Application;
using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Customers.Domain;
using AlSsareea.Modules.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Customers.Infrastructure;

internal sealed class CustomersService(
    CustomersDbContext db,
    ICustomerRepository repository,
    IClock clock,
    IOptions<CustomersOptions> options) : ICustomersService
{
    private CustomersOptions Options => options.Value;

    public async Task<CustomerOperationResult<CustomerResponse>> CreateCurrentAsync(Guid userId, CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (!Options.AllowCustomerSelfProfileCreation) return CustomerOperation.Failure<CustomerResponse>(CustomerOperationStatus.Disabled, "customers.creation_disabled");
        if (await db.Customers.IgnoreQueryFilters().AnyAsync(x => x.UserId == userId, cancellationToken))
            return CustomerOperation.Failure<CustomerResponse>(CustomerOperationStatus.Conflict, "customers.profile_exists");
        try
        {
            DateTime now = clock.UtcNow;
            Customer customer = Customer.Create(CustomerId.New(), userId, request.FirstName, request.LastName, request.DateOfBirth, now, userId, Options.DefaultLanguage, Options.DefaultCurrency);
            await repository.AddAsync(customer, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return CustomerOperation.Created(ToResponse(customer));
        }
        catch (DomainException) { return Invalid<CustomerResponse>(); }
        catch (DbUpdateException) { return Conflict<CustomerResponse>(); }
    }

    public async Task<CustomerOperationResult<CustomerResponse>> GetCurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        return customer is null ? NotFound<CustomerResponse>() : CustomerOperation.Success(ToResponse(customer));
    }

    public async Task<CustomerOperationResult<CustomerResponse>> UpdateCurrentAsync(Guid userId, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        if (customer is null) return NotFound<CustomerResponse>();
        if (customer.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<CustomerResponse>();
        try
        {
            customer.UpdateProfile(request.FirstName, request.LastName, request.DateOfBirth, clock.UtcNow, userId);
            return await SaveAsync(ToResponse(customer), cancellationToken);
        }
        catch (DomainException) { return Invalid<CustomerResponse>(); }
    }

    public async Task<CustomerOperationResult<IReadOnlyList<AddressResponse>>> GetCurrentAddressesAsync(Guid userId, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        return customer is null ? NotFound<IReadOnlyList<AddressResponse>>() : CustomerOperation.Success<IReadOnlyList<AddressResponse>>(MapAddresses(customer));
    }

    public async Task<CustomerOperationResult<AddressResponse>> GetCurrentAddressAsync(Guid userId, Guid addressId, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        CustomerAddress? address = customer?.Addresses.SingleOrDefault(x => x.Id.Value == addressId && x.DeletedAtUtc is null);
        return address is null ? NotFound<AddressResponse>() : CustomerOperation.Success(ToResponse(address));
    }

    public async Task<CustomerOperationResult<AddressResponse>> AddCurrentAddressAsync(Guid userId, AddressRequest request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        if (customer is null) return NotFound<AddressResponse>();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (request.IsDefault && !await ClearPersistedDefaultAsync(customer, cancellationToken)) return Conflict<AddressResponse>();
            CustomerAddress address = customer.AddAddress(CustomerAddressId.New(), request.Label, AddressType(request.AddressType), request.City, request.Area, request.Street, request.BuildingNumber, request.Floor, request.Apartment, request.PostalCode, request.PlaceId, Coordinate(request.Latitude, request.Longitude), request.DeliveryInstructions, request.IsDefault, clock.UtcNow, userId, Options.MaxAddressesPerCustomer);
            CustomerOperationResult<AddressResponse> result = await SaveAsync(ToResponse(address), cancellationToken, created: true);
            if (result.Status == CustomerOperationStatus.Created) await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DomainException) { return Invalid<AddressResponse>(); }
        catch (DbUpdateException) { return Conflict<AddressResponse>(); }
    }

    public async Task<CustomerOperationResult<AddressResponse>> UpdateCurrentAddressAsync(Guid userId, Guid addressId, AddressRequest request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        CustomerAddress? address = customer?.Addresses.SingleOrDefault(x => x.Id.Value == addressId && x.DeletedAtUtc is null);
        if (customer is null || address is null) return NotFound<AddressResponse>();
        if (request.ConcurrencyStamp is null || address.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<AddressResponse>();
        try
        {
            customer.UpdateAddress(address.Id, request.Label, AddressType(request.AddressType), request.City, request.Area, request.Street, request.BuildingNumber, request.Floor, request.Apartment, request.PostalCode, request.PlaceId, Coordinate(request.Latitude, request.Longitude), request.DeliveryInstructions, clock.UtcNow, userId);
            return await SaveAsync(ToResponse(address), cancellationToken);
        }
        catch (DomainException) { return Invalid<AddressResponse>(); }
    }

    public async Task<CustomerOperationResult<bool>> DeleteCurrentAddressAsync(Guid userId, Guid addressId, Guid concurrencyStamp, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        CustomerAddress? address = customer?.Addresses.SingleOrDefault(x => x.Id.Value == addressId && x.DeletedAtUtc is null);
        if (customer is null || address is null) return NotFound<bool>();
        if (address.ConcurrencyStamp != concurrencyStamp) return Conflict<bool>();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (address.IsDefault && !await ClearPersistedDefaultAsync(customer, cancellationToken)) return Conflict<bool>();
            customer.DeleteAddress(address.Id, clock.UtcNow, userId);
            CustomerOperationResult<bool> result = await SaveAsync(true, cancellationToken);
            if (result.Status == CustomerOperationStatus.Success) await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DomainException) { return Invalid<bool>(); }
    }

    public async Task<CustomerOperationResult<AddressResponse>> SetCurrentDefaultAddressAsync(Guid userId, Guid addressId, Guid concurrencyStamp, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        CustomerAddress? address = customer?.Addresses.SingleOrDefault(x => x.Id.Value == addressId && x.DeletedAtUtc is null);
        if (customer is null || address is null) return NotFound<AddressResponse>();
        if (address.ConcurrencyStamp != concurrencyStamp) return Conflict<AddressResponse>();
        if (address.IsDefault) return CustomerOperation.Success(ToResponse(address));
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (!await ClearPersistedDefaultAsync(customer, cancellationToken)) return Conflict<AddressResponse>();
            customer.SetDefaultAddress(address.Id, clock.UtcNow, userId);
            CustomerOperationResult<AddressResponse> result = await SaveAsync(ToResponse(address), cancellationToken);
            if (result.Status == CustomerOperationStatus.Success) await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DomainException) { return Invalid<AddressResponse>(); }
        catch (DbUpdateException) { return Conflict<AddressResponse>(); }
    }

    public async Task<CustomerOperationResult<PreferencesResponse>> GetCurrentPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        return customer is null ? NotFound<PreferencesResponse>() : CustomerOperation.Success(ToResponse(customer.Preferences));
    }

    public async Task<CustomerOperationResult<PreferencesResponse>> UpdateCurrentPreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        if (customer is null) return NotFound<PreferencesResponse>();
        if (customer.Preferences.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<PreferencesResponse>();
        try
        {
            customer.UpdatePreferences(request.PreferredLanguage, request.PreferredCurrency, request.AllowMarketingNotifications, request.AllowOrderStatusNotifications, request.AllowPromotionalNotifications, clock.UtcNow, userId);
            return await SaveAsync(ToResponse(customer.Preferences), cancellationToken);
        }
        catch (DomainException) { return Invalid<PreferencesResponse>(); }
    }

    public async Task<CustomerOperationResult<CustomerListResponse>> GetCustomersAsync(int page, int pageSize, string? sort, short? status, string? search, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Customer> query = db.Customers.AsNoTracking();
        if (status is not null) query = query.Where(x => (short)x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.DisplayName, $"%{term}%"));
        }
        int total = await query.CountAsync(cancellationToken);
        query = sort switch
        {
            "updatedAtUtc" => query.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id),
            "displayName" => query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id),
            "status" => query.OrderBy(x => x.Status).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id),
        };
        List<Customer> customers = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return CustomerOperation.Success(new CustomerListResponse(customers.Select(ToAdminResponse).ToArray(), page, pageSize, total));
    }

    public async Task<CustomerOperationResult<CustomerAdminResponse>> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(customerId), includeDeleted: true, cancellationToken);
        return customer is null ? NotFound<CustomerAdminResponse>() : CustomerOperation.Success(ToAdminResponse(customer));
    }

    public async Task<CustomerOperationResult<CustomerAdminResponse>> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, Guid actorUserId, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(customerId), cancellationToken: cancellationToken);
        if (customer is null) return NotFound<CustomerAdminResponse>();
        if (customer.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<CustomerAdminResponse>();
        try
        {
            customer.UpdateProfile(request.FirstName, request.LastName, request.DateOfBirth, clock.UtcNow, actorUserId);
            return await SaveAsync(ToAdminResponse(customer), cancellationToken);
        }
        catch (DomainException) { return Invalid<CustomerAdminResponse>(); }
    }

    public async Task<CustomerOperationResult<CustomerAdminResponse>> ChangeStatusAsync(Guid customerId, ChangeCustomerStatusRequest request, Guid actorUserId, string correlationId, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(customerId), cancellationToken: cancellationToken);
        if (customer is null) return NotFound<CustomerAdminResponse>();
        if (customer.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<CustomerAdminResponse>();
        try
        {
            switch ((CustomerStatus)request.Status)
            {
                case CustomerStatus.Active: customer.Activate(clock.UtcNow, actorUserId, correlationId); break;
                case CustomerStatus.Suspended: customer.Suspend(request.Reason ?? string.Empty, clock.UtcNow, actorUserId, correlationId); break;
                case CustomerStatus.Blocked: customer.Block(request.Reason ?? string.Empty, clock.UtcNow, actorUserId, correlationId); break;
                case CustomerStatus.Deleted: customer.Delete(request.Reason ?? string.Empty, clock.UtcNow, actorUserId, correlationId); break;
                default: return Invalid<CustomerAdminResponse>();
            }
            return await SaveAsync(ToAdminResponse(customer), cancellationToken);
        }
        catch (DomainException) { return Invalid<CustomerAdminResponse>(); }
    }

    public async Task<CustomerOperationResult<IReadOnlyList<StatusHistoryResponse>>> GetStatusHistoryAsync(Guid customerId, CancellationToken cancellationToken)
    {
        bool exists = await db.Customers.IgnoreQueryFilters().AnyAsync(x => x.Id == new CustomerId(customerId), cancellationToken);
        if (!exists) return NotFound<IReadOnlyList<StatusHistoryResponse>>();
        StatusHistoryResponse[] history = await db.CustomerStatusHistory.AsNoTracking()
            .Where(x => x.CustomerId == new CustomerId(customerId))
            .OrderByDescending(x => x.ChangedAtUtc).ThenBy(x => x.Id)
            .Select(x => new StatusHistoryResponse(x.Id.Value, (short)x.PreviousStatus, (short)x.NewStatus, x.Reason, x.ChangedAtUtc, x.ChangedByUserId, x.CorrelationId))
            .ToArrayAsync(cancellationToken);
        return CustomerOperation.Success<IReadOnlyList<StatusHistoryResponse>>(history);
    }

    public async Task<CustomerOperationResult<IReadOnlyList<AddressResponse>>> GetAddressesAsync(Guid customerId, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(customerId), includeDeleted: true, cancellationToken);
        return customer is null ? NotFound<IReadOnlyList<AddressResponse>>() : CustomerOperation.Success<IReadOnlyList<AddressResponse>>(MapAddresses(customer));
    }

    private async Task<CustomerOperationResult<T>> SaveAsync<T>(T value, CancellationToken cancellationToken, bool created = false)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created ? CustomerOperation.Created(value) : CustomerOperation.Success(value);
        }
        catch (DbUpdateConcurrencyException) { return Conflict<T>(); }
        catch (DbUpdateException) { return Conflict<T>(); }
    }

    private async Task<bool> ClearPersistedDefaultAsync(Customer customer, CancellationToken cancellationToken)
    {
        CustomerAddress? currentDefault = customer.Addresses.SingleOrDefault(x => x.DeletedAtUtc is null && x.IsDefault);
        if (currentDefault is null) return true;
        int affected = await db.CustomerAddresses
            .Where(x =>
                x.Id == currentDefault.Id &&
                x.CustomerId == customer.Id &&
                x.ConcurrencyStamp == currentDefault.ConcurrencyStamp &&
                x.IsDefault &&
                x.DeletedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
        return affected == 1;
    }

    private static GeoCoordinate? Coordinate(double? latitude, double? longitude)
    {
        if (latitude.HasValue != longitude.HasValue) throw new DomainException("Latitude and longitude must both be supplied or both omitted.");
        return latitude is null ? null : new GeoCoordinate(latitude.Value, longitude!.Value);
    }

    private static CustomerAddressType AddressType(short value)
        => Enum.IsDefined((CustomerAddressType)value) ? (CustomerAddressType)value : throw new DomainException("Address type is invalid.");

    private static AddressResponse[] MapAddresses(Customer customer) => customer.Addresses
        .Where(x => x.DeletedAtUtc is null).OrderByDescending(x => x.IsDefault).ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id.Value).Select(ToResponse).ToArray();

    private static CustomerResponse ToResponse(Customer x) => new(x.Id.Value, x.FirstName, x.LastName, x.DisplayName, x.DateOfBirth, (short)x.Status, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp);
    private static CustomerAdminResponse ToAdminResponse(Customer x) => new(x.Id.Value, x.UserId, x.FirstName, x.LastName, x.DisplayName, x.DateOfBirth, (short)x.Status, x.BlockReason, x.CreatedAtUtc, x.UpdatedAtUtc, x.DeletedAtUtc, x.ConcurrencyStamp);
    private static AddressResponse ToResponse(CustomerAddress x) => new(x.Id.Value, x.Label, (short)x.AddressType, x.City, x.Area, x.Street, x.BuildingNumber, x.Floor, x.Apartment, x.PostalCode, x.PlaceId, x.Location?.Latitude, x.Location?.Longitude, x.DeliveryInstructions, x.IsDefault, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp);
    private static PreferencesResponse ToResponse(CustomerPreference x) => new(x.PreferredLanguage, x.PreferredCurrency, x.AllowMarketingNotifications, x.AllowOrderStatusNotifications, x.AllowPromotionalNotifications, x.UpdatedAtUtc, x.ConcurrencyStamp);
    private static CustomerOperationResult<T> NotFound<T>() => CustomerOperation.Failure<T>(CustomerOperationStatus.NotFound, "customers.not_found");
    private static CustomerOperationResult<T> Conflict<T>() => CustomerOperation.Failure<T>(CustomerOperationStatus.Conflict, "customers.concurrency_or_conflict");
    private static CustomerOperationResult<T> Invalid<T>() => CustomerOperation.Failure<T>(CustomerOperationStatus.Invalid, "customers.validation_failed");
}
