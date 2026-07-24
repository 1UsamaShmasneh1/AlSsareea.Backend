using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Customers.Domain;

public sealed class CustomerAddress : Entity<CustomerAddressId>
{
    private CustomerAddress(CustomerAddressId id) : base(id)
    {
        Label = null!;
        City = null!;
        Street = null!;
    }

    internal CustomerAddress(CustomerAddressId id, CustomerId customerId, string label, CustomerAddressType type, string city, string? area, string street, string? buildingNumber, string? floor, string? apartment, string? postalCode, string? placeId, GeoCoordinate? location, string? deliveryInstructions, bool isDefault, DateTime now, Guid actor)
        : base(id)
    {
        CustomerId = customerId;
        Apply(label, type, city, area, street, buildingNumber, floor, apartment, postalCode, placeId, location, deliveryInstructions);
        IsDefault = isDefault;
        CreatedAtUtc = now;
        CreatedByUserId = actor;
        UpdatedAtUtc = now;
        UpdatedByUserId = actor;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public CustomerId CustomerId { get; private set; }
    public string Label { get; private set; } = null!;
    public CustomerAddressType AddressType { get; private set; }
    public string City { get; private set; } = null!;
    public string? Area { get; private set; }
    public string Street { get; private set; } = null!;
    public string? BuildingNumber { get; private set; }
    public string? Floor { get; private set; }
    public string? Apartment { get; private set; }
    public string? PostalCode { get; private set; }
    public string? PlaceId { get; private set; }
    public GeoCoordinate? Location { get; private set; }
    public string? DeliveryInstructions { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    internal void Update(string label, CustomerAddressType type, string city, string? area, string street, string? buildingNumber, string? floor, string? apartment, string? postalCode, string? placeId, GeoCoordinate? location, string? instructions, DateTime now, Guid actor)
    {
        EnsureActive();
        Apply(label, type, city, area, street, buildingNumber, floor, apartment, postalCode, placeId, location, instructions);
        Touch(now, actor);
    }

    internal void SetDefault(bool value, DateTime now, Guid actor)
    {
        EnsureActive();
        if (IsDefault == value) return;
        IsDefault = value;
        Touch(now, actor);
    }

    internal void Delete(DateTime now, Guid actor)
    {
        EnsureActive();
        IsDefault = false;
        DeletedAtUtc = now;
        DeletedByUserId = actor;
        Touch(now, actor);
    }

    private void Apply(string label, CustomerAddressType type, string city, string? area, string street, string? buildingNumber, string? floor, string? apartment, string? postalCode, string? placeId, GeoCoordinate? location, string? instructions)
    {
        if (!Enum.IsDefined(type)) throw new DomainException("Address type is invalid.");
        Label = CustomerDomainRules.Required(label, 100, nameof(label));
        AddressType = type;
        City = CustomerDomainRules.Required(city, 150, nameof(city));
        Area = CustomerDomainRules.Optional(area, 150, nameof(area));
        Street = CustomerDomainRules.Required(street, 200, nameof(street));
        BuildingNumber = CustomerDomainRules.Optional(buildingNumber, 50, nameof(buildingNumber));
        Floor = CustomerDomainRules.Optional(floor, 30, nameof(floor));
        Apartment = CustomerDomainRules.Optional(apartment, 30, nameof(apartment));
        PostalCode = CustomerDomainRules.Optional(postalCode, 20, nameof(postalCode));
        PlaceId = CustomerDomainRules.Optional(placeId, 300, nameof(placeId));
        Location = location;
        DeliveryInstructions = CustomerDomainRules.Optional(instructions, 1000, nameof(instructions));
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
        if (DeletedAtUtc is not null) throw new DomainException("Deleted addresses cannot be modified.");
    }
}
