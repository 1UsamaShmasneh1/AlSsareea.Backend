using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Merchants.Domain;

public abstract record MerchantDomainEvent(DateTime OccurredAtUtc) : IDomainEvent;
public sealed record MerchantCreatedDomainEvent(MerchantId MerchantId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantActivatedDomainEvent(MerchantId MerchantId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantSuspendedDomainEvent(MerchantId MerchantId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantRejectedDomainEvent(MerchantId MerchantId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantClosedDomainEvent(MerchantId MerchantId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantOwnerChangedDomainEvent(MerchantId MerchantId, Guid PreviousOwnerUserId, Guid OwnerUserId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantBranchCreatedDomainEvent(MerchantId MerchantId, MerchantBranchId BranchId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantBranchStatusChangedDomainEvent(MerchantId MerchantId, MerchantBranchId BranchId, MerchantBranchStatus Status, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantPrimaryBranchChangedDomainEvent(MerchantId MerchantId, MerchantBranchId BranchId, bool IsPrimary, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantBranchLocationChangedDomainEvent(MerchantId MerchantId, MerchantBranchId BranchId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record BranchServiceAreaAssignedDomainEvent(MerchantId MerchantId, MerchantBranchId BranchId, Guid ServiceAreaId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record BranchServiceAreaRemovedDomainEvent(MerchantId MerchantId, MerchantBranchId BranchId, Guid ServiceAreaId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantEmployeeAddedDomainEvent(MerchantId MerchantId, MerchantEmployeeId EmployeeId, Guid UserId, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantEmployeeStatusChangedDomainEvent(MerchantId MerchantId, MerchantEmployeeId EmployeeId, MerchantMembershipStatus Status, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
public sealed record MerchantEmployeeRoleChangedDomainEvent(MerchantId MerchantId, MerchantEmployeeId EmployeeId, MerchantMembershipRole Role, DateTime OccurredAtUtc) : MerchantDomainEvent(OccurredAtUtc);
