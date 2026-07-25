using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Merchants.Domain;

public sealed class MerchantEmployee : AggregateRoot<MerchantEmployeeId>
{
    private MerchantEmployee(MerchantEmployeeId id) : base(id) { }
    private MerchantEmployee(MerchantEmployeeId id, MerchantId merchantId, Guid userId, MerchantBranchId? branchId, MerchantMembershipRole role, MerchantMembershipStatus status, DateTime now) : base(id)
    {
        MerchantId = merchantId; UserId = MerchantRules.User(userId); BranchId = branchId;
        if (!Enum.IsDefined(role)) throw new DomainException("Membership role is invalid.");
        Role = role; Status = status; CreatedAtUtc = UpdatedAtUtc = now;
        JoinedAtUtc = status == MerchantMembershipStatus.Active ? now : null;
        ConcurrencyStamp = Guid.NewGuid();
    }
    public MerchantId MerchantId { get; private set; }
    public Guid UserId { get; private set; }
    public MerchantBranchId? BranchId { get; private set; }
    public MerchantMembershipRole Role { get; private set; }
    public MerchantMembershipStatus Status { get; private set; }
    public DateTime? JoinedAtUtc { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public DateTime? RemovedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    public static MerchantEmployee Create(MerchantEmployeeId id, MerchantId merchantId, Guid userId, MerchantBranchId? branchId, MerchantMembershipRole role, bool invited, DateTime now)
    {
        MerchantRules.Utc(now, nameof(now));
        MerchantEmployee value = new(id, merchantId, userId, branchId, role, invited ? MerchantMembershipStatus.Invited : MerchantMembershipStatus.Active, now);
        value.RaiseDomainEvent(new MerchantEmployeeAddedDomainEvent(merchantId, id, userId, now));
        return value;
    }
    public void Activate(DateTime now)
    {
        if (Status != MerchantMembershipStatus.Invited) throw new DomainException("Only an invited membership can be activated.");
        Status = MerchantMembershipStatus.Active; JoinedAtUtc ??= now; Touch(now); StatusEvent(now);
    }
    public void Suspend(DateTime now)
    {
        if (Status != MerchantMembershipStatus.Active) throw new DomainException("Only an active membership can be suspended.");
        Status = MerchantMembershipStatus.Suspended; SuspendedAtUtc = now; Touch(now); StatusEvent(now);
    }
    public void Remove(DateTime now)
    {
        if (Status == MerchantMembershipStatus.Removed) throw new DomainException("Membership is already removed.");
        Status = MerchantMembershipStatus.Removed; RemovedAtUtc = now; Touch(now); StatusEvent(now);
    }
    public void ChangeRole(MerchantMembershipRole role, DateTime now)
    {
        if (Status == MerchantMembershipStatus.Removed) throw new DomainException("Removed memberships cannot be modified.");
        if (!Enum.IsDefined(role)) throw new DomainException("Membership role is invalid.");
        if (Role == role) throw new DomainException("Membership already has this role.");
        Role = role; Touch(now); RaiseDomainEvent(new MerchantEmployeeRoleChangedDomainEvent(MerchantId, Id, role, now));
    }
    public void AssignBranch(MerchantBranchId? branchId, DateTime now)
    {
        if (Status == MerchantMembershipStatus.Removed) throw new DomainException("Removed memberships cannot be modified.");
        if (Role == MerchantMembershipRole.Owner && branchId is not null) throw new DomainException("Owners cannot be restricted to a branch.");
        BranchId = branchId; Touch(now);
    }
    private void Touch(DateTime now) { MerchantRules.Utc(now, nameof(now)); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    private void StatusEvent(DateTime now) => RaiseDomainEvent(new MerchantEmployeeStatusChangedDomainEvent(MerchantId, Id, Status, now));
}
