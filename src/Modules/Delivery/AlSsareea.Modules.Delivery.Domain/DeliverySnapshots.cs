using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Delivery.Domain;

public sealed class PickupSnapshot
{
    private PickupSnapshot() { }

    public PickupSnapshot(Guid merchantId, Guid? branchId, string address, string? contactName, string? phoneNumber, string? instructions, double? latitude, double? longitude)
    {
        if (merchantId == Guid.Empty || string.IsNullOrWhiteSpace(address)) throw new DomainException("Pickup snapshot is invalid.");
        ValidateCoordinates(latitude, longitude);
        MerchantId = merchantId;
        BranchId = branchId;
        Address = Required(address, DeliveryRules.AddressMaximumLength, "Pickup address");
        ContactName = Optional(contactName, DeliveryRules.ContactMaximumLength, "Pickup contact");
        PhoneNumber = Optional(phoneNumber, DeliveryRules.ContactMaximumLength, "Pickup phone number");
        Instructions = Optional(instructions, DeliveryRules.InstructionsMaximumLength, "Pickup instructions");
        Latitude = latitude;
        Longitude = longitude;
    }

    public Guid MerchantId { get; private set; }
    public Guid? BranchId { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string? ContactName { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Instructions { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    internal static void ValidateCoordinates(double? latitude, double? longitude)
    {
        if (latitude.HasValue != longitude.HasValue || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new DomainException("Delivery coordinates are invalid.");
    }

    internal static string Required(string value, int maximumLength, string name)
    {
        string normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength) throw new DomainException($"{name} is invalid.");
        return normalized;
    }

    internal static string? Optional(string? value, int maximumLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new DomainException($"{name} is invalid.");
        return normalized;
    }
}

public sealed class DropOffSnapshot
{
    private DropOffSnapshot() { }

    public DropOffSnapshot(Guid addressId, string address, string recipientName, string? phoneNumber, string? floor, string? instructions, double? latitude, double? longitude)
    {
        if (addressId == Guid.Empty) throw new DomainException("Drop-off address identifier is required.");
        PickupSnapshot.ValidateCoordinates(latitude, longitude);
        AddressId = addressId;
        Address = PickupSnapshot.Required(address, DeliveryRules.AddressMaximumLength, "Drop-off address");
        RecipientName = PickupSnapshot.Required(recipientName, DeliveryRules.RecipientNameMaximumLength, "Recipient name");
        PhoneNumber = PickupSnapshot.Optional(phoneNumber, DeliveryRules.ContactMaximumLength, "Recipient phone number");
        Floor = PickupSnapshot.Optional(floor, DeliveryRules.ContactMaximumLength, "Drop-off floor");
        Instructions = PickupSnapshot.Optional(instructions, DeliveryRules.InstructionsMaximumLength, "Drop-off instructions");
        Latitude = latitude;
        Longitude = longitude;
    }

    public Guid AddressId { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? Floor { get; private set; }
    public string? Instructions { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
}
