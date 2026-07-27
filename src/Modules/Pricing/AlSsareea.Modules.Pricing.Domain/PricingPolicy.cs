using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Pricing.Domain;

public sealed class PricingPolicy : AggregateRoot<PricingPolicyId>
{
    private readonly List<PricingRule> _rules = [];

    private PricingPolicy(PricingPolicyId id) : base(id)
    {
        Name = Currency = null!;
    }

    private PricingPolicy(
        PricingPolicyId id,
        string name,
        PricingScope scope,
        string currency,
        DateTime effectiveFromUtc,
        DateTime? effectiveUntilUtc,
        int priority,
        DateTime now) : base(id)
    {
        Name = PricingRules.Required(name, 200, nameof(name));
        ScopeType = scope.Type;
        MerchantId = scope.MerchantId;
        BranchId = scope.BranchId;
        ZoneId = scope.ZoneId;
        ScopeKey = $"{(short)scope.Type}:{scope.MerchantId?.ToString("N") ?? "-"}:{scope.BranchId?.ToString("N") ?? "-"}:{scope.ZoneId?.ToString("N") ?? "-"}";
        Currency = PricingRules.Currency(currency);
        PricingRules.Period(effectiveFromUtc, effectiveUntilUtc);
        PricingRules.Priority(priority);
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveUntilUtc = effectiveUntilUtc;
        Priority = priority;
        Status = PricingPolicyStatus.Draft;
        Version = 1;
        CreatedAtUtc = UpdatedAtUtc = now;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public string Name { get; private set; } = null!;
    public PricingScopeType ScopeType { get; private set; }
    public Guid? MerchantId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? ZoneId { get; private set; }
    public string ScopeKey { get; private set; } = null!;
    public PricingScope Scope => PricingScope.Create(ScopeType, MerchantId, BranchId, ZoneId);
    public string Currency { get; private set; } = null!;
    public PricingPolicyStatus Status { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveUntilUtc { get; private set; }
    public int Priority { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? DeactivatedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<PricingRule> Rules => _rules.AsReadOnly();

    public static PricingPolicy Create(
        PricingPolicyId id,
        string name,
        PricingScope scope,
        string currency,
        DateTime effectiveFromUtc,
        DateTime? effectiveUntilUtc,
        int priority,
        DateTime now)
    {
        PricingRules.Utc(now);
        var policy = new PricingPolicy(id, name, scope, currency, effectiveFromUtc, effectiveUntilUtc, priority, now);
        policy.RaiseDomainEvent(new PricingPolicyCreatedDomainEvent(id, now));
        return policy;
    }

    public void UpdateDraft(string name, DateTime effectiveFromUtc, DateTime? effectiveUntilUtc, int priority, DateTime now)
    {
        EnsureDraft();
        PricingRules.Period(effectiveFromUtc, effectiveUntilUtc);
        PricingRules.Priority(priority);
        Name = PricingRules.Required(name, 200, nameof(name));
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveUntilUtc = effectiveUntilUtc;
        Priority = priority;
        Touch(now);
    }

    public void ReplaceRules(IEnumerable<PricingRule> rules, DateTime now)
    {
        EnsureDraft();
        PricingRule[] values = [.. rules];
        if (values.Select(x => x.Id).Distinct().Count() != values.Length) throw new DomainException("Pricing rule IDs must be unique.");
        if (values.GroupBy(x => x.Type).Any(x => x.Key is not (PricingRuleType.FixedDelivery or PricingRuleType.DistanceDelivery or PricingRuleType.ZoneDelivery) && x.Count() > 1))
            throw new DomainException("A policy cannot contain duplicate logically equivalent rules.");
        _rules.Clear();
        _rules.AddRange(values.OrderByDescending(x => x.Priority).ThenBy(x => x.Id.Value));
        Touch(now);
    }

    public void Activate(DateTime now)
    {
        if (Status is not (PricingPolicyStatus.Draft or PricingPolicyStatus.Inactive)) throw new DomainException("Only draft or inactive policies can be activated.");
        if (_rules.Count == 0) throw new DomainException("A pricing policy must contain rules before activation.");
        Status = PricingPolicyStatus.Active;
        ActivatedAtUtc = now;
        DeactivatedAtUtc = null;
        Touch(now);
        RaiseDomainEvent(new PricingPolicyActivatedDomainEvent(Id, Version, now));
    }

    public void Deactivate(DateTime now)
    {
        if (Status != PricingPolicyStatus.Active) throw new DomainException("Only an active pricing policy can be deactivated.");
        Status = PricingPolicyStatus.Inactive;
        DeactivatedAtUtc = now;
        Touch(now);
        RaiseDomainEvent(new PricingPolicyDeactivatedDomainEvent(Id, Version, now));
    }

    public void Archive(DateTime now)
    {
        if (Status == PricingPolicyStatus.Archived) throw new DomainException("Pricing policy is already archived.");
        if (Status == PricingPolicyStatus.Active) throw new DomainException("An active pricing policy must be deactivated before archiving.");
        Status = PricingPolicyStatus.Archived;
        ArchivedAtUtc = now;
        Touch(now);
        RaiseDomainEvent(new PricingPolicyArchivedDomainEvent(Id, Version, now));
    }

    public bool IsEffectiveAt(DateTime atUtc) =>
        Status == PricingPolicyStatus.Active &&
        EffectiveFromUtc <= atUtc &&
        (!EffectiveUntilUtc.HasValue || EffectiveUntilUtc.Value > atUtc);

    private void EnsureDraft()
    {
        if (Status != PricingPolicyStatus.Draft) throw new DomainException("Only draft pricing policies can be edited.");
    }

    private void Touch(DateTime now)
    {
        PricingRules.Utc(now);
        UpdatedAtUtc = now;
        Version = checked(Version + 1);
        ConcurrencyStamp = Guid.NewGuid();
    }
}

public sealed class PricingRule : Entity<PricingRuleId>
{
    private PricingRule(PricingRuleId id) : base(id) { }

    private PricingRule(
        PricingRuleId id,
        PricingRuleType type,
        PricingCalculationKind kind,
        PricingCalculationBase calculationBase,
        int priority,
        long amountMinor,
        int percentageBasisPoints,
        long? thresholdMinor,
        long? minimumMinor,
        long? maximumMinor,
        int? includedDistanceMeters,
        int? maximumDistanceMeters,
        long? additionalFeePerKilometerMinor) : base(id)
    {
        Type = type;
        Kind = kind;
        CalculationBase = calculationBase;
        Priority = priority;
        AmountMinor = amountMinor;
        PercentageBasisPoints = percentageBasisPoints;
        ThresholdMinor = thresholdMinor;
        MinimumMinor = minimumMinor;
        MaximumMinor = maximumMinor;
        IncludedDistanceMeters = includedDistanceMeters;
        MaximumDistanceMeters = maximumDistanceMeters;
        AdditionalFeePerKilometerMinor = additionalFeePerKilometerMinor;
    }

    public PricingRuleType Type { get; private set; }
    public PricingCalculationKind Kind { get; private set; }
    public PricingCalculationBase CalculationBase { get; private set; }
    public int Priority { get; private set; }
    public long AmountMinor { get; private set; }
    public int PercentageBasisPoints { get; private set; }
    public long? ThresholdMinor { get; private set; }
    public long? MinimumMinor { get; private set; }
    public long? MaximumMinor { get; private set; }
    public int? IncludedDistanceMeters { get; private set; }
    public int? MaximumDistanceMeters { get; private set; }
    public long? AdditionalFeePerKilometerMinor { get; private set; }

    public static PricingRule Create(
        PricingRuleId id,
        PricingRuleType type,
        PricingCalculationKind kind,
        PricingCalculationBase calculationBase,
        int priority,
        long amountMinor,
        int percentageBasisPoints = 0,
        long? thresholdMinor = null,
        long? minimumMinor = null,
        long? maximumMinor = null,
        int? includedDistanceMeters = null,
        int? maximumDistanceMeters = null,
        long? additionalFeePerKilometerMinor = null)
    {
        PricingRules.Priority(priority);
        PricingRules.NonNegative(amountMinor, nameof(amountMinor));
        PricingRules.Percentage(percentageBasisPoints);
        PricingRules.OptionalNonNegative(thresholdMinor, nameof(thresholdMinor));
        PricingRules.OptionalNonNegative(minimumMinor, nameof(minimumMinor));
        PricingRules.OptionalNonNegative(maximumMinor, nameof(maximumMinor));
        if (minimumMinor.HasValue && maximumMinor.HasValue && minimumMinor > maximumMinor) throw new DomainException("Minimum fee cannot exceed maximum fee.");

        if (type == PricingRuleType.DistanceDelivery)
        {
            if (includedDistanceMeters is null or < 0 || maximumDistanceMeters is null or <= 0 || maximumDistanceMeters <= includedDistanceMeters)
                throw new DomainException("Distance rule limits are invalid.");
            if (additionalFeePerKilometerMinor is null or < 0) throw new DomainException("Distance fee must be non-negative.");
        }

        if (type is PricingRuleType.SmallOrderFee or PricingRuleType.MinimumOrder && thresholdMinor is null)
            throw new DomainException("Threshold is required.");
        if (kind == PricingCalculationKind.Percentage && percentageBasisPoints == 0)
            throw new DomainException("Percentage fee requires a positive percentage.");
        if (type != PricingRuleType.Tax && kind == PricingCalculationKind.Disabled)
            throw new DomainException("Only tax rules may be disabled.");

        return new(id, type, kind, calculationBase, priority, amountMinor, percentageBasisPoints, thresholdMinor, minimumMinor, maximumMinor, includedDistanceMeters, maximumDistanceMeters, additionalFeePerKilometerMinor);
    }

    public long Calculate(long calculationBaseMinor)
    {
        long value = Kind switch
        {
            PricingCalculationKind.Disabled => 0,
            PricingCalculationKind.Fixed => AmountMinor,
            PricingCalculationKind.Percentage => PricingMath.Percentage(calculationBaseMinor, PercentageBasisPoints),
            _ => throw new DomainException("Unsupported pricing calculation kind."),
        };
        return PricingMath.Cap(value, MinimumMinor, MaximumMinor);
    }

    public long CalculateDistance(int distanceMeters)
    {
        if (Type != PricingRuleType.DistanceDelivery) throw new DomainException("Rule is not distance based.");
        if (distanceMeters < 0) throw new DomainException("Distance cannot be negative.");
        if (distanceMeters > MaximumDistanceMeters) throw new DomainException("Maximum delivery distance exceeded.");
        int chargeable = Math.Max(0, distanceMeters - IncludedDistanceMeters!.Value);
        long segments = (chargeable + 999L) / 1_000L;
        long value = checked(AmountMinor + checked(segments * AdditionalFeePerKilometerMinor!.Value));
        return PricingMath.Cap(value, MinimumMinor, MaximumMinor);
    }
}

internal static class PricingRules
{
    public static string Required(string value, int max, string name)
    {
        string result = value?.Trim() ?? string.Empty;
        if (result.Length is 0 || result.Length > max) throw new DomainException($"{name} is invalid.");
        return result;
    }

    public static string Currency(string value)
    {
        string result = Required(value, 3, nameof(value)).ToUpperInvariant();
        if (result.Length != 3 || result.Any(x => x is < 'A' or > 'Z')) throw new DomainException("Currency must be a three-letter ISO-style code.");
        return result;
    }

    public static void Utc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new DomainException("Timestamp must be UTC.");
    }

    public static void Period(DateTime from, DateTime? until)
    {
        Utc(from);
        if (until.HasValue) Utc(until.Value);
        if (until.HasValue && until.Value <= from) throw new DomainException("Effective end must be later than effective start.");
    }

    public static void Priority(int value)
    {
        if (value is < 0 or > 1_000) throw new DomainException("Priority must be between 0 and 1000.");
    }

    public static void Percentage(int value)
    {
        if (value is < 0 or > PricingMath.MaximumBasisPoints) throw new DomainException("Percentage is outside the allowed range.");
    }

    public static void NonNegative(long value, string name)
    {
        if (value < 0) throw new DomainException($"{name} cannot be negative.");
    }

    public static void OptionalNonNegative(long? value, string name)
    {
        if (value < 0) throw new DomainException($"{name} cannot be negative.");
    }
}

public sealed record PricingPolicyCreatedDomainEvent(PricingPolicyId PolicyId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record PricingPolicyActivatedDomainEvent(PricingPolicyId PolicyId, int Version, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record PricingPolicyDeactivatedDomainEvent(PricingPolicyId PolicyId, int Version, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record PricingPolicyArchivedDomainEvent(PricingPolicyId PolicyId, int Version, DateTime OccurredAtUtc) : IDomainEvent;
