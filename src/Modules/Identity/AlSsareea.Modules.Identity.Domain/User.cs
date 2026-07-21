using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public sealed class User : AggregateRoot<UserId>
{
    private readonly List<UserRole> _roles = [];
    private readonly List<Device> _devices = [];
    private readonly List<LoginSession> _sessions = [];
    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<PasswordHistory> _passwordHistory = [];

    private User(UserId id, UserType userType, Email? email, PhoneNumber? phoneNumber, PasswordHash passwordHash, DateTime createdUtc, UserId? createdByUserId)
        : base(id)
    {
        UserType = userType;
        Email = email;
        NormalizedEmail = email?.Normalized;
        PhoneNumber = phoneNumber;
        NormalizedPhoneNumber = phoneNumber?.Normalized;
        PasswordHash = passwordHash;
        Status = UserStatus.PendingActivation;
        SecurityStamp = Guid.NewGuid();
        ConcurrencyStamp = Guid.NewGuid();
        LastPasswordChangedUtc = createdUtc;
        CreatedUtc = createdUtc;
        CreatedByUserId = createdByUserId;
    }

    public UserType UserType { get; private set; }
    public UserStatus Status { get; private set; }
    public Email? Email { get; private set; }
    public string? NormalizedEmail { get; private set; }
    public PhoneNumber? PhoneNumber { get; private set; }
    public string? NormalizedPhoneNumber { get; private set; }
    public PasswordHash PasswordHash { get; private set; }
    public Guid SecurityStamp { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }
    public DateTime LastPasswordChangedUtc { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? UpdatedUtc { get; private set; }
    public DateTime? DeletedUtc { get; private set; }
    public UserId? CreatedByUserId { get; private set; }
    public UserId? UpdatedByUserId { get; private set; }
    public UserId? DeletedByUserId { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();
    public IReadOnlyCollection<Device> Devices => _devices.AsReadOnly();
    public IReadOnlyCollection<LoginSession> Sessions => _sessions.AsReadOnly();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();
    public IReadOnlyCollection<PasswordHistory> PasswordHistory => _passwordHistory.AsReadOnly();

    public static User Create(UserId id, UserType userType, Email? email, PhoneNumber? phoneNumber, PasswordHash passwordHash, DateTime createdUtc, UserId? createdByUserId = null)
    {
        if (!Enum.IsDefined(userType)) throw new DomainException("User type is invalid.");
        if (email is null && phoneNumber is null) throw new DomainException("An email address or phone number is required.");
        DomainRules.RequireUtc(createdUtc, nameof(createdUtc));
        var user = new User(id, userType, email, phoneNumber, passwordHash, createdUtc, createdByUserId);
        user.RaiseDomainEvent(new UserCreatedDomainEvent(id, createdUtc));
        return user;
    }

    public void Activate(DateTime occurredUtc, UserId? actor = null) => ChangeStatus(UserStatus.Active, occurredUtc, actor, UserStatus.PendingActivation, UserStatus.Locked, UserStatus.Suspended);
    public void Lock(DateTime occurredUtc, DateTime? lockoutEndUtc = null, UserId? actor = null)
    {
        if (lockoutEndUtc is not null) { DomainRules.RequireUtc(lockoutEndUtc.Value, nameof(lockoutEndUtc)); if (lockoutEndUtc <= occurredUtc) throw new DomainException("Lockout end must be in the future."); }
        ChangeStatus(UserStatus.Locked, occurredUtc, actor, UserStatus.Active);
        LockoutEndUtc = lockoutEndUtc;
    }
    public void Suspend(DateTime occurredUtc, UserId? actor = null) => ChangeStatus(UserStatus.Suspended, occurredUtc, actor, UserStatus.Active, UserStatus.Locked);
    public void Disable(DateTime occurredUtc, UserId? actor = null) => ChangeStatus(UserStatus.Disabled, occurredUtc, actor, UserStatus.PendingActivation, UserStatus.Active, UserStatus.Locked, UserStatus.Suspended);
    public void SoftDelete(DateTime occurredUtc, UserId? actor = null)
    {
        ChangeStatus(UserStatus.Deleted, occurredUtc, actor, UserStatus.PendingActivation, UserStatus.Active, UserStatus.Locked, UserStatus.Suspended, UserStatus.Disabled);
        DeletedUtc = occurredUtc;
        DeletedByUserId = actor;
        RotateSecurityStamp();
    }

    public void ChangeEmail(Email? email, DateTime occurredUtc, UserId? actor = null)
    {
        EnsureNotDeleted();
        if (email is null && PhoneNumber is null) throw new DomainException("An email address or phone number is required.");
        Email = email; NormalizedEmail = email?.Normalized; Touch(occurredUtc, actor); RotateSecurityStamp();
        RaiseDomainEvent(new UserEmailChangedDomainEvent(Id, occurredUtc));
    }

    public void ChangePhoneNumber(PhoneNumber? phoneNumber, DateTime occurredUtc, UserId? actor = null)
    {
        EnsureNotDeleted();
        if (phoneNumber is null && Email is null) throw new DomainException("An email address or phone number is required.");
        PhoneNumber = phoneNumber; NormalizedPhoneNumber = phoneNumber?.Normalized; Touch(occurredUtc, actor); RotateSecurityStamp();
        RaiseDomainEvent(new UserPhoneNumberChangedDomainEvent(Id, occurredUtc));
    }

    public void ChangePassword(PasswordHash passwordHash, DateTime occurredUtc, UserId? actor = null)
    {
        EnsureNotDeleted(); DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc));
        _passwordHistory.Add(global::AlSsareea.Modules.Identity.Domain.PasswordHistory.Create(PasswordHistoryId.New(), Id, PasswordHash, LastPasswordChangedUtc, occurredUtc, occurredUtc));
        PasswordHash = passwordHash; LastPasswordChangedUtc = occurredUtc; Touch(occurredUtc, actor); RotateSecurityStamp();
        RaiseDomainEvent(new UserPasswordChangedDomainEvent(Id, occurredUtc));
    }

    public void AssignRole(RoleId roleId, DateTime assignedUtc, UserId? assignedByUserId = null)
    {
        EnsureNotDeleted(); DomainRules.RequireUtc(assignedUtc, nameof(assignedUtc));
        if (_roles.Any(x => x.RoleId == roleId)) throw new DomainException("Role is already assigned to the user.");
        _roles.Add(new UserRole(Id, roleId, assignedUtc, assignedByUserId));
        RaiseDomainEvent(new RoleAssignedToUserDomainEvent(Id, roleId, assignedUtc));
    }

    public void RemoveRole(RoleId roleId, DateTime occurredUtc)
    {
        UserRole role = _roles.SingleOrDefault(x => x.RoleId == roleId) ?? throw new DomainException("Role is not assigned to the user.");
        _roles.Remove(role); RaiseDomainEvent(new RoleRemovedFromUserDomainEvent(Id, roleId, occurredUtc));
    }

    public Device RegisterDevice(DeviceId id, DeviceIdentifier identifier, DevicePlatform platform, string? name, string? appVersion, string? operatingSystemVersion, DateTime occurredUtc)
    {
        if (_devices.Any(x => x.DeviceIdentifier == identifier)) throw new DomainException("Device identifier is already registered for this user.");
        Device device = Device.Register(id, Id, identifier, platform, name, appVersion, operatingSystemVersion, occurredUtc);
        _devices.Add(device); RaiseDomainEvent(new DeviceRegisteredDomainEvent(Id, id, occurredUtc)); return device;
    }

    private void ChangeStatus(UserStatus target, DateTime occurredUtc, UserId? actor, params UserStatus[] allowed)
    {
        DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc));
        if (!allowed.Contains(Status)) throw new DomainException($"Cannot transition user from {Status} to {target}.");
        UserStatus previous = Status; Status = target; Touch(occurredUtc, actor); RotateSecurityStamp();
        RaiseDomainEvent(new UserStatusChangedDomainEvent(Id, previous, target, occurredUtc));
    }

    private void Touch(DateTime occurredUtc, UserId? actor) { DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc)); UpdatedUtc = occurredUtc; UpdatedByUserId = actor; ConcurrencyStamp = Guid.NewGuid(); }
    private void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();
    private void EnsureNotDeleted() { if (Status == UserStatus.Deleted) throw new DomainException("Deleted users cannot be modified."); }
}

public sealed class UserRole
{
    internal UserRole(UserId userId, RoleId roleId, DateTime assignedUtc, UserId? assignedByUserId) { UserId = userId; RoleId = roleId; AssignedUtc = assignedUtc; AssignedByUserId = assignedByUserId; }
    public UserId UserId { get; private set; }
    public RoleId RoleId { get; private set; }
    public DateTime AssignedUtc { get; private set; }
    public UserId? AssignedByUserId { get; private set; }
}
