using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public abstract record IdentityDomainEvent(DateTime OccurredAtUtc) : IDomainEvent;
public sealed record UserCreatedDomainEvent(UserId UserId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record UserStatusChangedDomainEvent(UserId UserId, UserStatus PreviousStatus, UserStatus CurrentStatus, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record UserEmailChangedDomainEvent(UserId UserId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record UserPhoneNumberChangedDomainEvent(UserId UserId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record UserPasswordChangedDomainEvent(UserId UserId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record RoleAssignedToUserDomainEvent(UserId UserId, RoleId RoleId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record RoleRemovedFromUserDomainEvent(UserId UserId, RoleId RoleId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record PermissionAssignedToRoleDomainEvent(RoleId RoleId, PermissionId PermissionId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record PermissionRemovedFromRoleDomainEvent(RoleId RoleId, PermissionId PermissionId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record DeviceRegisteredDomainEvent(UserId UserId, DeviceId DeviceId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record DeviceRevokedDomainEvent(UserId UserId, DeviceId DeviceId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record SessionStartedDomainEvent(UserId UserId, LoginSessionId SessionId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record SessionRevokedDomainEvent(UserId UserId, LoginSessionId SessionId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
public sealed record RefreshTokenRevokedDomainEvent(UserId UserId, RefreshTokenId TokenId, DateTime OccurredAtUtc) : IdentityDomainEvent(OccurredAtUtc);
