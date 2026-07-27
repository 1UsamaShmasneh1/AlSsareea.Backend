using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Promotions.Domain;

public abstract record PromotionDomainEvent(DateTime OccurredAtUtc) : IDomainEvent;
public sealed record PromotionCreatedDomainEvent(PromotionId PromotionId, DateTime OccurredAtUtc) : PromotionDomainEvent(OccurredAtUtc);
public sealed record PromotionChangedDomainEvent(PromotionId PromotionId, DateTime OccurredAtUtc) : PromotionDomainEvent(OccurredAtUtc);
public sealed record PromotionActivatedDomainEvent(PromotionId PromotionId, DateTime OccurredAtUtc) : PromotionDomainEvent(OccurredAtUtc);
public sealed record PromotionSuspendedDomainEvent(PromotionId PromotionId, DateTime OccurredAtUtc) : PromotionDomainEvent(OccurredAtUtc);
public sealed record PromotionArchivedDomainEvent(PromotionId PromotionId, DateTime OccurredAtUtc) : PromotionDomainEvent(OccurredAtUtc);
public sealed record PromotionRedemptionRecordedDomainEvent(PromotionId PromotionId, PromotionRedemptionId RedemptionId, DateTime OccurredAtUtc) : PromotionDomainEvent(OccurredAtUtc);
