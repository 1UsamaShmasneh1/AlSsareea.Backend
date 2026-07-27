using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Promotions.Domain;

public sealed class Promotion : AggregateRoot<PromotionId>
{
    private Promotion(PromotionId id) : base(id)
    {
        InternalName = null!;
        DisplayName = null!;
        Validity = null!;
        Scope = null!;
        Benefit = null!;
        Funding = null!;
        UsageLimits = null!;
        Eligibility = null!;
    }

    private Promotion(
        PromotionId id,
        string internalName,
        LocalizedText displayName,
        LocalizedText? description,
        PromotionType type,
        int priority,
        StackabilityPolicy stackability,
        string? conflictGroup,
        FundingPolicy funding,
        ValidityPeriod validity,
        UsageLimits usageLimits,
        EligibilityRules eligibility,
        PromotionScope scope,
        DiscountBenefit benefit,
        CouponCode? couponCode,
        DateTime now) : base(id)
    {
        InternalName = PromotionRules.Required(internalName, 100, nameof(internalName));
        DisplayName = displayName;
        Description = description;
        ValidateConfiguration(type, scope, benefit, couponCode, eligibility);
        Type = type;
        Priority = ValidatePriority(priority);
        Stackability = stackability;
        ConflictGroup = NormalizeConflictGroup(conflictGroup);
        Funding = funding;
        Validity = validity;
        UsageLimits = usageLimits;
        Eligibility = eligibility;
        Scope = scope;
        Benefit = benefit;
        CouponCode = couponCode;
        Status = PromotionStatus.Draft;
        CreatedAtUtc = UpdatedAtUtc = now;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public string InternalName { get; private set; }
    public LocalizedText DisplayName { get; private set; }
    public LocalizedText? Description { get; private set; }
    public PromotionType Type { get; private set; }
    public PromotionStatus Status { get; private set; }
    public int Priority { get; private set; }
    public StackabilityPolicy Stackability { get; private set; }
    public string? ConflictGroup { get; private set; }
    public FundingPolicy Funding { get; private set; }
    public ValidityPeriod Validity { get; private set; }
    public UsageLimits UsageLimits { get; private set; }
    public EligibilityRules Eligibility { get; private set; }
    public PromotionScope Scope { get; private set; }
    public DiscountBenefit Benefit { get; private set; }
    public CouponCode? CouponCode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    public static Promotion Create(
        PromotionId id,
        string internalName,
        LocalizedText displayName,
        LocalizedText? description,
        PromotionType type,
        int priority,
        StackabilityPolicy stackability,
        string? conflictGroup,
        FundingPolicy funding,
        ValidityPeriod validity,
        UsageLimits usageLimits,
        EligibilityRules eligibility,
        PromotionScope scope,
        DiscountBenefit benefit,
        CouponCode? couponCode,
        DateTime now)
    {
        PromotionRules.Utc(now, nameof(now));
        Promotion promotion = new(id, internalName, displayName, description, type, priority, stackability, conflictGroup, funding, validity, usageLimits, eligibility, scope, benefit, couponCode, now);
        promotion.RaiseDomainEvent(new PromotionCreatedDomainEvent(id, now));
        return promotion;
    }

    public void Update(
        string internalName,
        LocalizedText displayName,
        LocalizedText? description,
        int priority,
        StackabilityPolicy stackability,
        string? conflictGroup,
        FundingPolicy funding,
        ValidityPeriod validity,
        UsageLimits usageLimits,
        EligibilityRules eligibility,
        PromotionScope scope,
        DiscountBenefit benefit,
        CouponCode? couponCode,
        DateTime now)
    {
        if (Status == PromotionStatus.Archived) throw new DomainException("Archived promotions cannot be modified.");
        if (Status == PromotionStatus.Active) throw new DomainException("Active promotions must be suspended before configuration changes.");
        ValidateConfiguration(Type, scope, benefit, couponCode, eligibility);
        InternalName = PromotionRules.Required(internalName, 100, nameof(internalName));
        DisplayName = displayName;
        Description = description;
        Priority = ValidatePriority(priority);
        Stackability = stackability;
        ConflictGroup = NormalizeConflictGroup(conflictGroup);
        Funding = funding;
        Validity = validity;
        UsageLimits = usageLimits;
        Eligibility = eligibility;
        Scope = scope;
        Benefit = benefit;
        CouponCode = couponCode;
        Touch(now);
        RaiseDomainEvent(new PromotionChangedDomainEvent(Id, now));
    }

    public void Activate(DateTime now)
    {
        PromotionRules.Utc(now, nameof(now));
        if (Status is not (PromotionStatus.Draft or PromotionStatus.Suspended)) throw new DomainException("Promotion cannot be activated from its current state.");
        if (now >= Validity.EndsAtUtc) throw new DomainException("Expired promotion cannot be activated.");
        Status = PromotionStatus.Active;
        ActivatedAtUtc ??= now;
        SuspendedAtUtc = null;
        Touch(now);
        RaiseDomainEvent(new PromotionActivatedDomainEvent(Id, now));
    }

    public void Suspend(DateTime now)
    {
        if (Status != PromotionStatus.Active) throw new DomainException("Only an active promotion can be suspended.");
        Status = PromotionStatus.Suspended;
        SuspendedAtUtc = now;
        Touch(now);
        RaiseDomainEvent(new PromotionSuspendedDomainEvent(Id, now));
    }

    public void Archive(DateTime now)
    {
        if (Status == PromotionStatus.Archived) throw new DomainException("Promotion is already archived.");
        Status = PromotionStatus.Archived;
        ArchivedAtUtc = now;
        Touch(now);
        RaiseDomainEvent(new PromotionArchivedDomainEvent(Id, now));
    }

    public bool IsApplicableAt(DateTime now) => Status == PromotionStatus.Active && Validity.Contains(now);

    private void Touch(DateTime now)
    {
        PromotionRules.Utc(now, nameof(now));
        UpdatedAtUtc = now;
        ConcurrencyStamp = Guid.NewGuid();
    }
    private static int ValidatePriority(int value) => value is < -100000 or > 100000 ? throw new DomainException("Priority is outside the supported range.") : value;
    private static string? NormalizeConflictGroup(string? value) => PromotionRules.Optional(value?.ToUpperInvariant(), 64, nameof(value));
    private static void ValidateConfiguration(PromotionType type, PromotionScope scope, DiscountBenefit benefit, CouponCode? coupon, EligibilityRules eligibility)
    {
        if (!Enum.IsDefined(type)) throw new DomainException("Promotion type is invalid.");
        if (type == PromotionType.Coupon && coupon is null || type != PromotionType.Coupon && coupon is not null) throw new DomainException("Coupon configuration does not match promotion type.");
        if (type == PromotionType.ProductDiscount && scope.Type != PromotionScopeType.Product ||
            type == PromotionType.CategoryDiscount && scope.Type != PromotionScopeType.Category ||
            type == PromotionType.MerchantDiscount && scope.Type is not (PromotionScopeType.Merchant or PromotionScopeType.Branch) ||
            type == PromotionType.FreeDelivery && benefit.Kind != DiscountKind.FreeDelivery ||
            type != PromotionType.FreeDelivery && benefit.Kind == DiscountKind.FreeDelivery)
            throw new DomainException("Promotion scope or benefit does not match its type.");
        if (type == PromotionType.OrderThresholdDiscount && eligibility.MinimumSubtotalMinor is null)
            throw new DomainException("Order-threshold promotion requires a minimum subtotal.");
    }
}
