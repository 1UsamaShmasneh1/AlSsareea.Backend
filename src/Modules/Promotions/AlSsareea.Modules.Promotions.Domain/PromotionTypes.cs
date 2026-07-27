using System.Globalization;
using System.Text.RegularExpressions;
using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Promotions.Domain;

public readonly record struct PromotionId
{
    public PromotionId(Guid value) => Value = value == Guid.Empty ? throw new DomainException("PromotionId cannot be empty.") : value;
    public Guid Value { get; }
    public static PromotionId New() => new(Guid.NewGuid());
}

public readonly record struct PromotionRedemptionId
{
    public PromotionRedemptionId(Guid value) => Value = value == Guid.Empty ? throw new DomainException("PromotionRedemptionId cannot be empty.") : value;
    public Guid Value { get; }
    public static PromotionRedemptionId New() => new(Guid.NewGuid());
}

public readonly record struct PromotionAuditId
{
    public PromotionAuditId(Guid value) => Value = value == Guid.Empty ? throw new DomainException("PromotionAuditId cannot be empty.") : value;
    public Guid Value { get; }
    public static PromotionAuditId New() => new(Guid.NewGuid());
}

public enum PromotionType : short { Coupon = 1, ProductDiscount, CategoryDiscount, MerchantDiscount, OrderThresholdDiscount, FreeDelivery }
public enum PromotionStatus : short { Draft = 1, Active, Suspended, Expired, Archived }
public enum StackabilityPolicy : short { Stackable = 1, NonStackable, Exclusive }
public enum FundingSource : short { Platform = 1, Merchant, Shared }
public enum DiscountKind : short { FixedAmount = 1, Percentage, FreeDelivery }
public enum PromotionScopeType : short { Global = 1, Merchant, Branch, Category, Product }

public readonly record struct Currency
{
    private static readonly Regex Pattern = new("^[A-Z]{3}$", RegexOptions.CultureInvariant);
    public Currency(string value)
    {
        string normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Pattern.IsMatch(normalized)) throw new DomainException("Currency must be a three-letter ISO code.");
        Value = normalized;
    }
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record LocalizedText
{
    private LocalizedText() { Arabic = English = null!; }
    public LocalizedText(string arabic, string? hebrew, string english)
    {
        Arabic = PromotionRules.Required(arabic, 200, nameof(arabic));
        Hebrew = PromotionRules.Optional(hebrew, 200, nameof(hebrew));
        English = PromotionRules.Required(english, 200, nameof(english));
    }
    public string Arabic { get; private init; }
    public string? Hebrew { get; private init; }
    public string English { get; private init; }
}

public sealed record ValidityPeriod
{
    private ValidityPeriod() { }
    public ValidityPeriod(DateTime startsAtUtc, DateTime endsAtUtc)
    {
        PromotionRules.Utc(startsAtUtc, nameof(startsAtUtc));
        PromotionRules.Utc(endsAtUtc, nameof(endsAtUtc));
        if (endsAtUtc <= startsAtUtc) throw new DomainException("Promotion end must be after its start.");
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
    }
    public DateTime StartsAtUtc { get; private init; }
    public DateTime EndsAtUtc { get; private init; }
    public bool Contains(DateTime timestampUtc) => timestampUtc >= StartsAtUtc && timestampUtc < EndsAtUtc;
}

public sealed record UsageLimits
{
    private UsageLimits() { }
    public UsageLimits(long? globalLimit, long? perCustomerLimit, long? budgetLimitMinor, int? maximumRedemptionsPerOrder)
    {
        if (globalLimit is <= 0 || perCustomerLimit is <= 0 || budgetLimitMinor is <= 0 || maximumRedemptionsPerOrder is <= 0)
            throw new DomainException("Usage limits must be positive when specified.");
        if (globalLimit is not null && perCustomerLimit > globalLimit) throw new DomainException("Per-customer limit cannot exceed global limit.");
        GlobalLimit = globalLimit;
        PerCustomerLimit = perCustomerLimit;
        BudgetLimitMinor = budgetLimitMinor;
        MaximumRedemptionsPerOrder = maximumRedemptionsPerOrder;
    }
    public long? GlobalLimit { get; private init; }
    public long? PerCustomerLimit { get; private init; }
    public long? BudgetLimitMinor { get; private init; }
    public int? MaximumRedemptionsPerOrder { get; private init; }
    public bool IsUnlimited => GlobalLimit is null && PerCustomerLimit is null && BudgetLimitMinor is null;
    public static UsageLimits Unlimited => new(null, null, null, null);
}

public sealed record FundingPolicy
{
    private FundingPolicy() { }
    public FundingPolicy(FundingSource source, int platformShareBasisPoints, int merchantShareBasisPoints)
    {
        if (!Enum.IsDefined(source)) throw new DomainException("Funding source is invalid.");
        if (platformShareBasisPoints is < 0 or > 10000 || merchantShareBasisPoints is < 0 or > 10000 ||
            platformShareBasisPoints + merchantShareBasisPoints != 10000)
            throw new DomainException("Funding shares must total 10000 basis points.");
        if (source == FundingSource.Platform && (platformShareBasisPoints != 10000 || merchantShareBasisPoints != 0) ||
            source == FundingSource.Merchant && (platformShareBasisPoints != 0 || merchantShareBasisPoints != 10000) ||
            source == FundingSource.Shared && (platformShareBasisPoints == 0 || merchantShareBasisPoints == 0))
            throw new DomainException("Funding shares do not match the funding source.");
        Source = source;
        PlatformShareBasisPoints = platformShareBasisPoints;
        MerchantShareBasisPoints = merchantShareBasisPoints;
    }
    public FundingSource Source { get; private init; }
    public int PlatformShareBasisPoints { get; private init; }
    public int MerchantShareBasisPoints { get; private init; }
    public static FundingPolicy Platform => new(FundingSource.Platform, 10000, 0);
    public static FundingPolicy Merchant => new(FundingSource.Merchant, 0, 10000);
}

public sealed record DiscountBenefit
{
    private DiscountBenefit() { Currency = new Currency("ILS"); }
    public DiscountBenefit(DiscountKind kind, Currency currency, long value, long? maximumDiscountMinor = null)
    {
        if (!Enum.IsDefined(kind)) throw new DomainException("Discount kind is invalid.");
        if (value < 0 || maximumDiscountMinor is < 0) throw new DomainException("Discount values cannot be negative.");
        if (kind == DiscountKind.FixedAmount && value == 0) throw new DomainException("Fixed discount must be positive.");
        if (kind == DiscountKind.Percentage && value is <= 0 or > 10000) throw new DomainException("Percentage discount must be between 1 and 10000 basis points.");
        if (kind == DiscountKind.FreeDelivery && (value != 0 || maximumDiscountMinor is not null)) throw new DomainException("Free delivery cannot define an amount or cap.");
        Kind = kind;
        Currency = currency;
        Value = value;
        MaximumDiscountMinor = maximumDiscountMinor;
    }
    public DiscountKind Kind { get; private init; }
    public Currency Currency { get; private init; }
    public long Value { get; private init; }
    public long? MaximumDiscountMinor { get; private init; }

    public long Calculate(long eligibleAmountMinor, long deliveryFeeMinor)
    {
        if (eligibleAmountMinor < 0 || deliveryFeeMinor < 0) throw new DomainException("Evaluation amounts cannot be negative.");
        long discount = Kind switch
        {
            DiscountKind.FixedAmount => Value,
            DiscountKind.Percentage => eligibleAmountMinor / 10000 * Value + eligibleAmountMinor % 10000 * Value / 10000,
            DiscountKind.FreeDelivery => deliveryFeeMinor,
            _ => 0,
        };
        if (MaximumDiscountMinor is not null) discount = Math.Min(discount, MaximumDiscountMinor.Value);
        return Math.Min(discount, Kind == DiscountKind.FreeDelivery ? deliveryFeeMinor : eligibleAmountMinor);
    }
}

public sealed record PromotionScope
{
    private PromotionScope() { TargetIds = []; }
    public PromotionScope(PromotionScopeType type, IEnumerable<Guid>? targetIds, Guid? merchantId = null)
    {
        if (!Enum.IsDefined(type)) throw new DomainException("Promotion scope is invalid.");
        Guid[] ids = (targetIds ?? []).Distinct().Order().ToArray();
        if (ids.Any(x => x == Guid.Empty)) throw new DomainException("Scope identifiers cannot be empty.");
        if (type == PromotionScopeType.Global && ids.Length != 0 || type != PromotionScopeType.Global && ids.Length == 0)
            throw new DomainException("Promotion scope targets are inconsistent.");
        if (merchantId == Guid.Empty) throw new DomainException("Scope merchant identifier cannot be empty.");
        Guid? owner = type == PromotionScopeType.Merchant && ids.Length == 1 ? ids[0] : merchantId;
        if (type is PromotionScopeType.Branch or PromotionScopeType.Category or PromotionScopeType.Product && owner is null)
            throw new DomainException("Merchant-owned promotion scopes require a merchant identifier.");
        if (type == PromotionScopeType.Global && owner is not null)
            throw new DomainException("Global promotion scope cannot have a merchant identifier.");
        Type = type;
        TargetIds = ids;
        MerchantId = owner;
    }
    public PromotionScopeType Type { get; private init; }
    public Guid[] TargetIds { get; private init; }
    public Guid? MerchantId { get; private init; }
    public static PromotionScope Global => new(PromotionScopeType.Global, null);
}

public sealed record EligibilityRules
{
    private EligibilityRules() { }
    public EligibilityRules(long? minimumSubtotalMinor, Guid? customerId, bool firstOrderOnly)
    {
        if (minimumSubtotalMinor is < 0 || customerId == Guid.Empty) throw new DomainException("Eligibility rules are invalid.");
        MinimumSubtotalMinor = minimumSubtotalMinor;
        CustomerId = customerId;
        FirstOrderOnly = firstOrderOnly;
    }
    public long? MinimumSubtotalMinor { get; private init; }
    public Guid? CustomerId { get; private init; }
    public bool FirstOrderOnly { get; private init; }
    public static EligibilityRules None => new(null, null, false);
}

public readonly record struct CouponCode
{
    private static readonly Regex Pattern = new("^[A-Z0-9][A-Z0-9_-]{2,63}$", RegexOptions.CultureInvariant);
    public CouponCode(string value)
    {
        string normalized = Regex.Replace(value?.Trim().ToUpperInvariant() ?? string.Empty, @"\s+", string.Empty);
        if (!Pattern.IsMatch(normalized)) throw new DomainException("Coupon code is invalid.");
        Value = normalized;
    }
    public string Value { get; }
    public override string ToString() => Value;
}

internal static class PromotionRules
{
    internal static string Required(string? value, int maximum, string name)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximum) throw new DomainException($"{name} is required and must not exceed {maximum} characters.");
        return normalized;
    }
    internal static string? Optional(string? value, int maximum, string name)
    {
        string? normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximum) throw new DomainException($"{name} must not exceed {maximum} characters.");
        return normalized;
    }
    internal static void Utc(DateTime value, string name) { if (value.Kind != DateTimeKind.Utc) throw new DomainException($"{name} must be UTC."); }
}
