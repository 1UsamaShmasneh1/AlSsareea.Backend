using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Customers.Domain;

public sealed class Customer : AggregateRoot<CustomerId>
{
    private readonly List<CustomerAddress> _addresses = [];
    private readonly List<CustomerStatusHistory> _statusHistory = [];

    private Customer(CustomerId id) : base(id)
    {
        FirstName = null!;
        LastName = null!;
        DisplayName = null!;
        Preferences = null!;
    }

    private Customer(CustomerId id, Guid userId, string firstName, string lastName, DateOnly? dateOfBirth, DateTime now, Guid actor, string language, string currency)
        : base(id)
    {
        UserId = CustomerDomainRules.Actor(userId);
        SetProfile(firstName, lastName, dateOfBirth, now);
        Status = CustomerStatus.Active;
        CreatedAtUtc = now;
        CreatedByUserId = actor;
        UpdatedAtUtc = now;
        UpdatedByUserId = actor;
        ConcurrencyStamp = Guid.NewGuid();
        Preferences = CustomerPreference.Create(CustomerPreferenceId.New(), id, language, currency, now);
    }

    public Guid UserId { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public DateOnly? DateOfBirth { get; private set; }
    public CustomerStatus Status { get; private set; }
    public string? BlockReason { get; private set; }
    public DateTime? BlockedAtUtc { get; private set; }
    public Guid? BlockedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public CustomerPreference Preferences { get; private set; } = null!;
    public IReadOnlyCollection<CustomerAddress> Addresses => _addresses.AsReadOnly();
    public IReadOnlyCollection<CustomerStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public static Customer Create(CustomerId id, Guid userId, string firstName, string lastName, DateOnly? dateOfBirth, DateTime now, Guid actor, string defaultLanguage = "ar", string defaultCurrency = "ILS")
    {
        CustomerDomainRules.Utc(now, nameof(now));
        Customer customer = new(id, userId, firstName, lastName, dateOfBirth, now, CustomerDomainRules.Actor(actor), defaultLanguage, defaultCurrency);
        customer.RaiseDomainEvent(new CustomerCreatedDomainEvent(id, now));
        return customer;
    }

    public void UpdateProfile(string firstName, string lastName, DateOnly? dateOfBirth, DateTime now, Guid actor)
    {
        EnsureActive();
        SetProfile(firstName, lastName, dateOfBirth, now);
        Touch(now, actor);
        RaiseDomainEvent(new CustomerProfileUpdatedDomainEvent(Id, now));
    }

    public CustomerAddress AddAddress(CustomerAddressId id, string label, CustomerAddressType type, string city, string? area, string street, string? buildingNumber, string? floor, string? apartment, string? postalCode, string? placeId, GeoCoordinate? location, string? instructions, bool requestedDefault, DateTime now, Guid actor, int maximumAddresses = 20)
    {
        EnsureActive();
        if (_addresses.Count(x => x.DeletedAtUtc is null) >= maximumAddresses) throw new DomainException($"A customer may have at most {maximumAddresses} active addresses.");
        bool makeDefault = !_addresses.Any(x => x.DeletedAtUtc is null) || requestedDefault;
        if (makeDefault) ClearDefault(now, actor);
        CustomerAddress address = new(id, Id, label, type, city, area, street, buildingNumber, floor, apartment, postalCode, placeId, location, instructions, makeDefault, now, actor);
        _addresses.Add(address);
        Touch(now, actor);
        RaiseDomainEvent(new CustomerAddressAddedDomainEvent(Id, id, now));
        if (makeDefault) RaiseDomainEvent(new CustomerDefaultAddressChangedDomainEvent(Id, id, now));
        return address;
    }

    public void UpdateAddress(CustomerAddressId addressId, string label, CustomerAddressType type, string city, string? area, string street, string? buildingNumber, string? floor, string? apartment, string? postalCode, string? placeId, GeoCoordinate? location, string? instructions, DateTime now, Guid actor)
    {
        CustomerAddress address = FindAddress(addressId);
        address.Update(label, type, city, area, street, buildingNumber, floor, apartment, postalCode, placeId, location, instructions, now, actor);
        Touch(now, actor);
        RaiseDomainEvent(new CustomerAddressUpdatedDomainEvent(Id, addressId, now));
    }

    public void SetDefaultAddress(CustomerAddressId addressId, DateTime now, Guid actor)
    {
        CustomerAddress address = FindAddress(addressId);
        if (address.IsDefault) return;
        ClearDefault(now, actor);
        address.SetDefault(true, now, actor);
        Touch(now, actor);
        RaiseDomainEvent(new CustomerDefaultAddressChangedDomainEvent(Id, addressId, now));
    }

    public void DeleteAddress(CustomerAddressId addressId, DateTime now, Guid actor)
    {
        CustomerAddress address = FindAddress(addressId);
        bool wasDefault = address.IsDefault;
        address.Delete(now, actor);
        if (wasDefault)
        {
            CustomerAddress? replacement = _addresses.Where(x => x.DeletedAtUtc is null).OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id.Value).FirstOrDefault();
            replacement?.SetDefault(true, now, actor);
            RaiseDomainEvent(new CustomerDefaultAddressChangedDomainEvent(Id, replacement?.Id, now));
        }
        Touch(now, actor);
        RaiseDomainEvent(new CustomerAddressDeletedDomainEvent(Id, addressId, now));
    }

    public void UpdatePreferences(string language, string currency, bool marketing, bool orderStatus, bool promotional, DateTime now, Guid actor)
    {
        EnsureActive();
        Preferences.Update(language, currency, marketing, orderStatus, promotional, now);
        Touch(now, actor);
        RaiseDomainEvent(new CustomerPreferencesUpdatedDomainEvent(Id, now));
    }

    public void Activate(DateTime now, Guid actor, string correlationId) => ChangeStatus(CustomerStatus.Active, null, now, actor, correlationId);
    public void Suspend(string reason, DateTime now, Guid actor, string correlationId) => ChangeStatus(CustomerStatus.Suspended, reason, now, actor, correlationId);
    public void Block(string reason, DateTime now, Guid actor, string correlationId) => ChangeStatus(CustomerStatus.Blocked, reason, now, actor, correlationId);
    public void Delete(string reason, DateTime now, Guid actor, string correlationId) => ChangeStatus(CustomerStatus.Deleted, reason, now, actor, correlationId);

    private void ChangeStatus(CustomerStatus target, string? reason, DateTime now, Guid actor, string correlationId)
    {
        CustomerDomainRules.Utc(now, nameof(now));
        actor = CustomerDomainRules.Actor(actor);
        if (Status == CustomerStatus.Deleted) throw new DomainException("Deleted customer status is terminal.");
        if (Status == target) throw new DomainException("A no-op customer status transition is not allowed.");
        if (!Enum.IsDefined(target) || target == 0) throw new DomainException("Customer status is invalid.");
        string? normalizedReason = target is CustomerStatus.Suspended or CustomerStatus.Blocked or CustomerStatus.Deleted
            ? CustomerDomainRules.Required(reason, 1000, nameof(reason))
            : CustomerDomainRules.Optional(reason, 1000, nameof(reason));
        string normalizedCorrelation = CustomerDomainRules.Required(correlationId, 128, nameof(correlationId));
        CustomerStatus previous = Status;
        Status = target;
        BlockReason = target == CustomerStatus.Blocked ? normalizedReason : null;
        BlockedAtUtc = target == CustomerStatus.Blocked ? now : null;
        BlockedByUserId = target == CustomerStatus.Blocked ? actor : null;
        if (target == CustomerStatus.Deleted) { DeletedAtUtc = now; DeletedByUserId = actor; }
        Touch(now, actor);
        _statusHistory.Add(new CustomerStatusHistory(CustomerStatusHistoryId.New(), Id, previous, target, normalizedReason, now, actor, normalizedCorrelation));
        RaiseDomainEvent(new CustomerStatusChangedDomainEvent(Id, previous, target, now));
    }

    private void SetProfile(string firstName, string lastName, DateOnly? dateOfBirth, DateTime now)
    {
        FirstName = CustomerDomainRules.Required(firstName, 100, nameof(firstName));
        LastName = CustomerDomainRules.Required(lastName, 100, nameof(lastName));
        DisplayName = $"{FirstName} {LastName}";
        if (dateOfBirth > DateOnly.FromDateTime(now)) throw new DomainException("Date of birth cannot be in the future.");
        DateOfBirth = dateOfBirth;
    }

    private CustomerAddress FindAddress(CustomerAddressId id)
    {
        EnsureActive();
        return _addresses.SingleOrDefault(x => x.Id == id && x.DeletedAtUtc is null) ?? throw new DomainException("Customer address was not found.");
    }

    private void ClearDefault(DateTime now, Guid actor)
    {
        foreach (CustomerAddress address in _addresses.Where(x => x.DeletedAtUtc is null && x.IsDefault)) address.SetDefault(false, now, actor);
    }

    private void Touch(DateTime now, Guid actor)
    {
        CustomerDomainRules.Utc(now, nameof(now));
        UpdatedAtUtc = now;
        UpdatedByUserId = CustomerDomainRules.Actor(actor);
        ConcurrencyStamp = Guid.NewGuid();
    }

    private void EnsureActive()
    {
        if (Status == CustomerStatus.Deleted) throw new DomainException("Deleted customers cannot be modified.");
    }
}
