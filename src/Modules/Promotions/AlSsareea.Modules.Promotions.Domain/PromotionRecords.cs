using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Promotions.Domain;

public sealed class PromotionRedemption : AggregateRoot<PromotionRedemptionId>
{
    private PromotionRedemption(PromotionRedemptionId id) : base(id) { ExternalReference = Currency = null!; }
    private PromotionRedemption(PromotionRedemptionId id, PromotionId promotionId, Guid? customerId, string externalReference, long discountAmountMinor, Currency currency, DateTime occurredAtUtc) : base(id)
    {
        if (customerId == Guid.Empty || discountAmountMinor < 0) throw new DomainException("Redemption data is invalid.");
        PromotionId = promotionId;
        CustomerId = customerId;
        ExternalReference = PromotionRules.Required(externalReference, 128, nameof(externalReference));
        DiscountAmountMinor = discountAmountMinor;
        Currency = currency.Value;
        OccurredAtUtc = occurredAtUtc;
    }
    public PromotionId PromotionId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string ExternalReference { get; private set; }
    public long DiscountAmountMinor { get; private set; }
    public string Currency { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public static PromotionRedemption Create(PromotionRedemptionId id, PromotionId promotionId, Guid? customerId, string externalReference, long discountAmountMinor, Currency currency, DateTime now)
    {
        PromotionRules.Utc(now, nameof(now));
        PromotionRedemption value = new(id, promotionId, customerId, externalReference, discountAmountMinor, currency, now);
        value.RaiseDomainEvent(new PromotionRedemptionRecordedDomainEvent(promotionId, id, now));
        return value;
    }
}

public sealed class PromotionAudit : Entity<PromotionAuditId>
{
    private PromotionAudit(PromotionAuditId id) : base(id) { Action = null!; }
    private PromotionAudit(PromotionAuditId id, PromotionId promotionId, Guid actorUserId, string action, DateTime occurredAtUtc) : base(id)
    {
        if (actorUserId == Guid.Empty) throw new DomainException("Audit actor is required.");
        PromotionId = promotionId;
        ActorUserId = actorUserId;
        Action = PromotionRules.Required(action, 80, nameof(action));
        OccurredAtUtc = occurredAtUtc;
    }
    public PromotionId PromotionId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public static PromotionAudit Create(PromotionAuditId id, PromotionId promotionId, Guid actorUserId, string action, DateTime now)
    {
        PromotionRules.Utc(now, nameof(now));
        return new(id, promotionId, actorUserId, action, now);
    }
}
