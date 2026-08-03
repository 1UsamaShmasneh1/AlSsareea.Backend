using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Orders.Domain;

public sealed class CustomerSnapshot
{
    private CustomerSnapshot() { }
    public CustomerSnapshot(Guid customerId, string displayName, string? phoneNumber, string preferredLanguage)
    {
        if (customerId == Guid.Empty || string.IsNullOrWhiteSpace(displayName)) throw new DomainException("Customer snapshot is invalid.");
        CustomerId = customerId; DisplayName = displayName.Trim(); PhoneNumber = Normalize(phoneNumber); PreferredLanguage = preferredLanguage.Trim().ToLowerInvariant();
        if (PreferredLanguage is not ("ar" or "he" or "en")) throw new DomainException("Preferred language is invalid.");
    }
    public Guid CustomerId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string PreferredLanguage { get; private set; } = string.Empty;
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DeliveryAddressSnapshot
{
    private DeliveryAddressSnapshot() { }
    public DeliveryAddressSnapshot(Guid addressId, string label, string city, string? area, string street, string? buildingNumber, string? floor, string? apartment, string? deliveryInstructions, double? latitude, double? longitude, string? placeId, string? formattedAddress)
    {
        if (addressId == Guid.Empty || string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(street)) throw new DomainException("Delivery address snapshot is invalid.");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) throw new DomainException("Delivery coordinates are invalid.");
        AddressId = addressId; Label = label.Trim(); City = city.Trim(); Area = N(area); Street = street.Trim(); BuildingNumber = N(buildingNumber); Floor = N(floor); Apartment = N(apartment); DeliveryInstructions = N(deliveryInstructions); Latitude = latitude; Longitude = longitude; PlaceId = N(placeId); FormattedAddress = N(formattedAddress);
    }
    public Guid AddressId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string? Area { get; private set; }
    public string Street { get; private set; } = string.Empty;
    public string? BuildingNumber { get; private set; }
    public string? Floor { get; private set; }
    public string? Apartment { get; private set; }
    public string? DeliveryInstructions { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? PlaceId { get; private set; }
    public string? FormattedAddress { get; private set; }
    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class MerchantSnapshot
{
    private MerchantSnapshot() { }
    public MerchantSnapshot(Guid merchantId, Guid? branchId, string merchantDisplayName, string? branchDisplayName, string? branchAddress, string? branchPhoneNumber)
    {
        if (merchantId == Guid.Empty || string.IsNullOrWhiteSpace(merchantDisplayName)) throw new DomainException("Merchant snapshot is invalid.");
        MerchantId = merchantId; BranchId = branchId; MerchantDisplayName = merchantDisplayName.Trim(); BranchDisplayName = N(branchDisplayName); BranchAddress = N(branchAddress); BranchPhoneNumber = N(branchPhoneNumber);
    }
    public Guid MerchantId { get; private set; }
    public Guid? BranchId { get; private set; }
    public string MerchantDisplayName { get; private set; } = string.Empty;
    public string? BranchDisplayName { get; private set; }
    public string? BranchAddress { get; private set; }
    public string? BranchPhoneNumber { get; private set; }
    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record OrderItemInput(Guid ProductId, int ProductVersion, Guid? VariantId, string ProductName, string? VariantName, string? Sku, int Quantity, long UnitBasePriceMinor, long UnitOptionsPriceMinor, long UnitDiscountMinor, long UnitFinalPriceMinor, long LineSubtotalMinor, long LineDiscountMinor, long LineTotalMinor, string? CustomerNote, IReadOnlyList<OrderItemOptionInput> Options);
public sealed record OrderItemOptionInput(Guid OptionGroupId, Guid OptionId, string OptionGroupName, string OptionName, int Quantity, long UnitPriceAdjustmentMinor, long TotalPriceAdjustmentMinor);
public sealed record OrderPricingInput(long SubtotalMinor, long OptionsTotalMinor, long ProductDiscountMinor, long CouponDiscountMinor, long DeliveryDiscountMinor, long DeliveryFeeMinor, long ServiceFeeMinor, long PlatformFeeMinor, long SmallOrderFeeMinor, long TaxMinor, long TotalMinor, string Currency, string? PricingReference, DateTime CalculatedAtUtc);
