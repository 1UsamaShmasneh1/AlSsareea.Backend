using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Merchants.Domain;

namespace AlSsareea.UnitTests.Merchants;

public sealed class MerchantDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateStartsPendingAndRaisesEvent()
    {
        Merchant merchant = CreateMerchant();
        Assert.Equal(MerchantStatus.PendingApproval, merchant.Status);
        Assert.Equal(UserId, merchant.OwnerUserId);
        Assert.Contains(merchant.DomainEvents, x => x is MerchantCreatedDomainEvent);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateRejectsInvalidName(string name) =>
        Assert.Throws<DomainException>(() => Merchant.Create(MerchantId.New(), name, "Shop", null, null, null, "owner@example.com", "+970599000000", UserId, Now));

    [Fact]
    public void LifecycleEnforcesTransitionsAndReasons()
    {
        Merchant merchant = CreateMerchant();
        merchant.Activate(Now.AddMinutes(1));
        Assert.Equal(MerchantStatus.Active, merchant.Status);
        Assert.Throws<DomainException>(() => merchant.Activate(Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => merchant.Suspend("", Now.AddMinutes(2)));
        merchant.Suspend("maintenance", Now.AddMinutes(2));
        Assert.Equal(MerchantStatus.Suspended, merchant.Status);
        Assert.Contains(merchant.DomainEvents, x => x is MerchantSuspendedDomainEvent);
    }

    [Fact]
    public void RejectOnlyAllowsPendingAndRequiresReason()
    {
        Merchant merchant = CreateMerchant();
        Assert.Throws<DomainException>(() => merchant.Reject("", Now.AddMinutes(1)));
        merchant.Reject("documentation incomplete", Now.AddMinutes(1));
        Assert.Equal(MerchantStatus.Rejected, merchant.Status);
        Assert.Throws<DomainException>(() => merchant.Activate(Now.AddMinutes(2)));
    }

    [Fact]
    public void ClosedMerchantCannotReactivateOrChangeOwner()
    {
        Merchant merchant = CreateMerchant();
        merchant.Close("business ended", Now.AddMinutes(1));
        Assert.Throws<DomainException>(() => merchant.Activate(Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => merchant.ChangeOwner(Guid.NewGuid(), Now.AddMinutes(2)));
    }

    [Fact]
    public void ChangeOwnerRaisesEvent()
    {
        Merchant merchant = CreateMerchant();
        Guid next = Guid.NewGuid();
        merchant.ChangeOwner(next, Now.AddMinutes(1));
        Assert.Equal(next, merchant.OwnerUserId);
        Assert.Contains(merchant.DomainEvents, x => x is MerchantOwnerChangedDomainEvent);
    }

    private static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static Merchant CreateMerchant() => Merchant.Create(MerchantId.New(), "Legal Shop", "Shop", null, null, null, "owner@example.com", "+970599000000", UserId, Now);
}

public sealed class MerchantBranchDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    public void CoordinatesAreValidated(double latitude, double longitude) =>
        Assert.Throws<DomainException>(() => new GeoCoordinate(latitude, longitude));

    [Fact]
    public void TimeZoneIsValidated() =>
        Assert.Throws<DomainException>(() => CreateBranch("Not/AZone"));

    [Fact]
    public void ActivationRequiresActiveMerchant()
    {
        MerchantBranch branch = CreateBranch();
        Assert.Throws<DomainException>(() => branch.Activate(false, Now.AddMinutes(1)));
        branch.Activate(true, Now.AddMinutes(1));
        Assert.Equal(MerchantBranchStatus.Active, branch.Status);
    }

    [Fact]
    public void BranchLifecycleRejectsInvalidTransitions()
    {
        MerchantBranch branch = CreateBranch();
        branch.Activate(true, Now.AddMinutes(1));
        branch.TemporarilyClose("holiday", Now.AddMinutes(2));
        Assert.Equal(MerchantBranchStatus.TemporarilyClosed, branch.Status);
        branch.Reopen(true, Now.AddMinutes(3));
        branch.Suspend("review", Now.AddMinutes(4));
        Assert.Throws<DomainException>(() => branch.Reopen(true, Now.AddMinutes(5)));
        branch.Close("closed", Now.AddMinutes(5));
        Assert.False(branch.IsPrimary);
    }

    [Fact]
    public void ClosedBranchCannotBecomePrimary()
    {
        MerchantBranch branch = CreateBranch();
        branch.Close("closed", Now.AddMinutes(1));
        Assert.Throws<DomainException>(() => branch.SetPrimary(true, Now.AddMinutes(2)));
    }

    [Fact]
    public void WeeklyHoursRejectOverlap()
    {
        MerchantBranch branch = CreateBranch();
        Assert.Throws<DomainException>(() => branch.ReplaceBusinessHours(Schedule(
            DayOfWeek.Monday,
            [new OpeningPeriod(new TimeOnly(9, 0), new TimeOnly(12, 0)), new OpeningPeriod(new TimeOnly(11, 0), new TimeOnly(14, 0))]), Now));
    }

    [Fact]
    public void AvailabilityUsesTimeZoneAndOverridePrecedence()
    {
        MerchantBranch branch = CreateBranch("Asia/Jerusalem");
        branch.ReplaceBusinessHours(Schedule(DayOfWeek.Saturday, [new OpeningPeriod(new TimeOnly(12, 0), new TimeOnly(15, 0))]), Now);
        branch.Activate(true, Now.AddMinutes(1));
        DateTime saturdayUtc = new(2026, 7, 25, 10, 30, 0, DateTimeKind.Utc);
        Assert.True(branch.GetAvailability(saturdayUtc).IsOpen);
        branch.AddClosure(new DateOnly(2026, 7, 25), new DateOnly(2026, 7, 25), "holiday", Now.AddMinutes(2));
        BranchAvailability availability = branch.GetAvailability(saturdayUtc);
        Assert.False(availability.IsOpen);
        Assert.Equal("exceptional-closure", availability.Source);
    }

    [Fact]
    public void SpecialHoursOverrideWeeklyHours()
    {
        MerchantBranch branch = CreateBranch("Asia/Jerusalem");
        branch.ReplaceBusinessHours(Schedule(DayOfWeek.Saturday, [new OpeningPeriod(new TimeOnly(8, 0), new TimeOnly(9, 0))]), Now);
        branch.SetSpecialHours(new DateOnly(2026, 7, 25), [new OpeningPeriod(new TimeOnly(12, 0), new TimeOnly(14, 0))], "event", Now.AddMinutes(1));
        branch.Activate(true, Now.AddMinutes(2));
        BranchAvailability value = branch.GetAvailability(new DateTime(2026, 7, 25, 10, 30, 0, DateTimeKind.Utc));
        Assert.True(value.IsOpen);
        Assert.Equal("special-hours", value.Source);
    }

    [Fact]
    public void ServiceAreaAssignmentRejectsDuplicatesAndSupportsRemoval()
    {
        MerchantBranch branch = CreateBranch();
        Guid id = Guid.NewGuid();
        branch.AssignServiceArea(id, Now.AddMinutes(1));
        Assert.Throws<DomainException>(() => branch.AssignServiceArea(id, Now.AddMinutes(2)));
        branch.RemoveServiceArea(id, Now.AddMinutes(2));
        Assert.Empty(branch.ServiceAreas);
    }

    private static MerchantBranch CreateBranch(string zone = "Asia/Jerusalem") =>
        MerchantBranch.Create(MerchantBranchId.New(), MerchantId.New(), "Central", "CTR", "+970599000000", "branch@example.com",
            BranchAddress.Create("Ramallah", null, "Main Street", "1", null), new GeoCoordinate(31.9, 35.2), zone, true, Now);

    private static IEnumerable<(DayOfWeek, bool, IEnumerable<OpeningPeriod>)> Schedule(DayOfWeek openDay, IEnumerable<OpeningPeriod> periods) =>
        Enum.GetValues<DayOfWeek>().Select(day => (day, day != openDay, day == openDay ? periods : []));
}

public sealed class MerchantEmployeeDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ActiveMembershipCanBeSuspendedAndRemoved()
    {
        MerchantEmployee employee = MerchantEmployee.Create(MerchantEmployeeId.New(), MerchantId.New(), Guid.NewGuid(), null, MerchantMembershipRole.Employee, false, Now);
        employee.Suspend(Now.AddMinutes(1));
        employee.Remove(Now.AddMinutes(2));
        Assert.Equal(MerchantMembershipStatus.Removed, employee.Status);
        Assert.Throws<DomainException>(() => employee.ChangeRole(MerchantMembershipRole.Manager, Now.AddMinutes(3)));
    }

    [Fact]
    public void InvitedMembershipCanBeActivatedOnce()
    {
        MerchantEmployee employee = MerchantEmployee.Create(MerchantEmployeeId.New(), MerchantId.New(), Guid.NewGuid(), null, MerchantMembershipRole.Employee, true, Now);
        employee.Activate(Now.AddMinutes(1));
        Assert.Equal(MerchantMembershipStatus.Active, employee.Status);
        Assert.Throws<DomainException>(() => employee.Activate(Now.AddMinutes(2)));
    }

    [Fact]
    public void OwnerCannotBeBranchRestricted()
    {
        MerchantEmployee owner = MerchantEmployee.Create(MerchantEmployeeId.New(), MerchantId.New(), Guid.NewGuid(), null, MerchantMembershipRole.Owner, false, Now);
        Assert.Throws<DomainException>(() => owner.AssignBranch(MerchantBranchId.New(), Now.AddMinutes(1)));
    }
}
