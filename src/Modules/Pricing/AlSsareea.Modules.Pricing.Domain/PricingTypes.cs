using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Pricing.Domain;

public readonly record struct PricingPolicyId
{
    public PricingPolicyId(Guid value)
    {
        if (value == Guid.Empty) throw new DomainException("Pricing policy ID cannot be empty.");
        Value = value;
    }

    public Guid Value { get; }
    public static PricingPolicyId New() => new(Guid.NewGuid());
}

public readonly record struct PricingRuleId
{
    public PricingRuleId(Guid value)
    {
        if (value == Guid.Empty) throw new DomainException("Pricing rule ID cannot be empty.");
        Value = value;
    }

    public Guid Value { get; }
    public static PricingRuleId New() => new(Guid.NewGuid());
}

public enum PricingPolicyStatus : short { Draft = 1, Active = 2, Inactive = 3, Archived = 4 }
public enum PricingScopeType : short { Global = 1, ServiceZone = 2, Merchant = 3, MerchantBranch = 4 }
public enum PricingRuleType : short { FixedDelivery = 1, DistanceDelivery = 2, ZoneDelivery = 3, ServiceFee = 4, PlatformFee = 5, SmallOrderFee = 6, MinimumOrder = 7, Tax = 8 }
public enum PricingCalculationKind : short { Disabled = 0, Fixed = 1, Percentage = 2 }
public enum PricingCalculationBase : short { ItemsSubtotal = 1, ItemsSubtotalPlusDelivery = 2, PreTaxTotal = 3 }

public sealed record PricingScope(PricingScopeType Type, Guid? MerchantId, Guid? BranchId, Guid? ZoneId)
{
    public static PricingScope Create(PricingScopeType type, Guid? merchantId, Guid? branchId, Guid? zoneId)
    {
        bool valid = type switch
        {
            PricingScopeType.Global => merchantId is null && branchId is null && zoneId is null,
            PricingScopeType.ServiceZone => zoneId.HasValue && zoneId.Value != Guid.Empty && merchantId is null && branchId is null,
            PricingScopeType.Merchant => merchantId is not null && merchantId != Guid.Empty && branchId is null && zoneId is null,
            PricingScopeType.MerchantBranch => merchantId is not null && merchantId != Guid.Empty && branchId is not null && branchId != Guid.Empty && zoneId is null,
            _ => false,
        };
        if (!valid) throw new DomainException("Pricing scope is inconsistent.");
        return new(type, merchantId, branchId, zoneId);
    }

    public int Specificity => Type switch
    {
        PricingScopeType.MerchantBranch => 4,
        PricingScopeType.Merchant => 3,
        PricingScopeType.ServiceZone => 2,
        _ => 1,
    };
}

public static class PricingMath
{
    public const int MaximumBasisPoints = 10_000;

    public static long Add(params long[] values)
    {
        long result = 0;
        foreach (long value in values) result = checked(result + value);
        return result;
    }

    public static long Percentage(long amountMinor, int basisPoints)
    {
        if (amountMinor < 0) throw new DomainException("Amount cannot be negative.");
        if (basisPoints is < 0 or > MaximumBasisPoints) throw new DomainException("Percentage is outside the allowed range.");
        long product = checked(amountMinor * basisPoints);
        return checked((product + 5_000) / 10_000);
    }

    public static long Cap(long amount, long? minimum, long? maximum)
    {
        if (minimum.HasValue && amount < minimum.Value) amount = minimum.Value;
        if (maximum.HasValue && amount > maximum.Value) amount = maximum.Value;
        return amount;
    }
}
