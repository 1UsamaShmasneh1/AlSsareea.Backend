using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Drivers.Domain;

namespace AlSsareea.UnitTests.Drivers;

public sealed class DriverDomainTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StrongIdsRejectEmptyAndUseValueEquality()
    {
        Assert.Throws<DomainException>(() => new DriverId(Guid.Empty)); Guid value = Guid.NewGuid(); Assert.Equal(new DriverId(value), new DriverId(value));
        Assert.Throws<DomainException>(() => new VehicleId(Guid.Empty)); Assert.Throws<DomainException>(() => new DriverDocumentId(Guid.Empty)); Assert.Throws<DomainException>(() => new DriverZoneAssignmentId(Guid.Empty)); Assert.Throws<DomainException>(() => new DriverShiftId(Guid.Empty)); Assert.Throws<DomainException>(() => new DriverViolationId(Guid.Empty)); Assert.Throws<DomainException>(() => new DriverSuspensionId(Guid.Empty));
    }

    [Fact]
    public void NewDriverStartsPendingOfflineAndCannotGoOnline()
    {
        Driver driver = Create(); Assert.Equal(DriverStatus.PendingReview, driver.Status); Assert.Equal(DriverActivationStatus.NotSubmitted, driver.ActivationStatus); Assert.Equal(AvailabilityStatus.Offline, driver.AvailabilityStatus); Assert.Throws<DomainException>(() => driver.GoOnline(Now.AddMinutes(1)));
    }

    [Fact]
    public void EligibleDriverCanGoOnlineTakeBreakAndReturn()
    {
        Driver driver = Eligible(); driver.GoOnline(Now.AddMinutes(10)); Assert.Equal(AvailabilityStatus.Online, driver.AvailabilityStatus); driver.StartBreak(Now.AddMinutes(11)); Assert.Equal(AvailabilityStatus.OnBreak, driver.AvailabilityStatus); driver.EndBreak(Now.AddMinutes(12)); Assert.Equal(AvailabilityStatus.Online, driver.AvailabilityStatus); driver.GoOffline(Now.AddMinutes(13)); driver.GoOffline(Now.AddMinutes(14)); Assert.Equal(AvailabilityStatus.Offline, driver.AvailabilityStatus);
    }

    [Fact]
    public void RepeatingOnlineAndOfflineDoesNotChangeStampOrRaiseAnotherEvent()
    {
        Driver driver = Eligible(); driver.ClearDomainEvents(); driver.GoOnline(Now.AddMinutes(10)); driver.ClearDomainEvents(); Guid onlineStamp = driver.ConcurrencyStamp;
        Assert.False(driver.GoOnline(Now.AddMinutes(11))); Assert.Equal(onlineStamp, driver.ConcurrencyStamp); Assert.Empty(driver.DomainEvents);
        Assert.True(driver.GoOffline(Now.AddMinutes(12))); driver.ClearDomainEvents(); Guid offlineStamp = driver.ConcurrencyStamp;
        Assert.False(driver.GoOffline(Now.AddMinutes(13))); Assert.Equal(offlineStamp, driver.ConcurrencyStamp); Assert.Empty(driver.DomainEvents);
    }

    [Fact]
    public void EligibilityRejectsUnapprovedDriverMissingZoneAndMissingOrUnapprovedPrimaryVehicle()
    {
        Driver unapproved = Create(); Assert.Throws<DomainException>(() => unapproved.GoOnline(Now.AddMinutes(10)));

        Driver withoutZone = Activated(); Vehicle vehicle = AddApprovedPrimaryVehicle(withoutZone); AddRequiredDocuments(withoutZone);
        Assert.Throws<DomainException>(() => withoutZone.GoOnline(Now.AddMinutes(10)));

        Driver withoutVehicle = Activated(); withoutVehicle.AssignZone(Guid.NewGuid(), true, Guid.NewGuid(), Now.AddMinutes(6)); AddRequiredDocuments(withoutVehicle);
        Assert.Throws<DomainException>(() => withoutVehicle.GoOnline(Now.AddMinutes(10)));

        Driver pendingVehicle = Activated(); pendingVehicle.AddVehicle(VehicleType.Car, "Toyota", "Corolla", 2024, "White", "23-456-78", "IL", true, Now.AddMinutes(4)); pendingVehicle.AssignZone(Guid.NewGuid(), true, Guid.NewGuid(), Now.AddMinutes(6)); AddRequiredDocuments(pendingVehicle);
        Assert.Throws<DomainException>(() => pendingVehicle.GoOnline(Now.AddMinutes(10)));
        Assert.True(vehicle.IsPrimary);
    }

    [Fact]
    public void EligibilityRejectsMissingRejectedReplacedExpiredAndBoundaryDocuments()
    {
        Driver missing = EligibleWithoutDocuments(); AddApprovedDocument(missing, DocumentType.DrivingLicense, Now.AddDays(10)); AddApprovedDocument(missing, DocumentType.VehicleRegistration, Now.AddDays(10));
        Assert.Throws<DomainException>(() => missing.GoOnline(Now.AddMinutes(10)));

        Driver rejected = EligibleWithoutDocuments(); DriverDocument rejectedLicense = rejected.SubmitDocument(DocumentType.DrivingLicense, Guid.NewGuid(), Now.AddDays(-1), Now.AddDays(10), Now.AddMinutes(7)); rejectedLicense.Reject(Guid.NewGuid(), "invalid", Now.AddMinutes(8)); AddApprovedDocument(rejected, DocumentType.VehicleRegistration, Now.AddDays(10)); AddApprovedDocument(rejected, DocumentType.VehicleInsurance, Now.AddDays(10));
        Assert.Throws<DomainException>(() => rejected.GoOnline(Now.AddMinutes(10)));

        Driver replaced = EligibleWithoutDocuments(); AddApprovedDocument(replaced, DocumentType.DrivingLicense, Now.AddDays(10)); replaced.SubmitDocument(DocumentType.DrivingLicense, Guid.NewGuid(), Now, Now.AddDays(20), Now.AddMinutes(9)); AddApprovedDocument(replaced, DocumentType.VehicleRegistration, Now.AddDays(10)); AddApprovedDocument(replaced, DocumentType.VehicleInsurance, Now.AddDays(10));
        Assert.Throws<DomainException>(() => replaced.GoOnline(Now.AddMinutes(10)));

        Driver expired = EligibleWithoutDocuments(); AddApprovedDocument(expired, DocumentType.DrivingLicense, Now.AddMinutes(9)); AddApprovedDocument(expired, DocumentType.VehicleRegistration, Now.AddDays(10)); AddApprovedDocument(expired, DocumentType.VehicleInsurance, Now.AddDays(10));
        Assert.Throws<DomainException>(() => expired.GoOnline(Now.AddMinutes(10)));

        Driver expiresNow = EligibleWithoutDocuments(); AddApprovedDocument(expiresNow, DocumentType.DrivingLicense, Now.AddMinutes(10)); AddApprovedDocument(expiresNow, DocumentType.VehicleRegistration, Now.AddDays(10)); AddApprovedDocument(expiresNow, DocumentType.VehicleInsurance, Now.AddDays(10));
        Assert.Throws<DomainException>(() => expiresNow.GoOnline(Now.AddMinutes(10)));
    }

    [Fact]
    public void CurrentVehicleTypesUseOneExplicitExtensibleDocumentPolicy()
    {
        DocumentType[] expected = [DocumentType.DrivingLicense, DocumentType.VehicleRegistration, DocumentType.VehicleInsurance];
        foreach (VehicleType type in Enum.GetValues<VehicleType>()) Assert.Equal(expected, DriverEligibilityPolicy.RequiredDocuments(type));
    }

    [Fact]
    public void CapacityAndPrimaryVehicleRulesAreEnforced()
    {
        Driver driver = Create(); Assert.Throws<DomainException>(() => driver.ChangeCapacity(0, Now.AddMinutes(1))); Vehicle first = driver.AddVehicle(VehicleType.Car, "Toyota", "Corolla", 2024, "White", "12-345-67", "IL", true, Now.AddMinutes(2)); Assert.Throws<DomainException>(() => driver.AddVehicle(VehicleType.Car, null, null, 2024, null, "99-999-99", "IL", true, Now.AddMinutes(3))); first.Approve(Now.AddMinutes(4)); driver.SetPrimaryVehicle(first.Id, Now.AddMinutes(5)); Assert.True(first.IsPrimary);
    }

    [Fact]
    public void DocumentsRequireValidDatesReviewAndReplacement()
    {
        Driver driver = Create(); Assert.Throws<DomainException>(() => driver.SubmitDocument(DocumentType.DrivingLicense, Guid.NewGuid(), Now.AddDays(1), Now, Now)); DriverDocument document = driver.SubmitDocument(DocumentType.DrivingLicense, Guid.NewGuid(), Now.AddDays(-10), Now.AddDays(10), Now); Assert.Throws<DomainException>(() => document.Reject(Guid.NewGuid(), "", Now.AddMinutes(1))); document.Approve(Guid.NewGuid(), Now.AddMinutes(1)); DriverDocument replacement = driver.SubmitDocument(DocumentType.DrivingLicense, Guid.NewGuid(), Now, Now.AddDays(20), Now.AddMinutes(2)); Assert.Equal(DocumentStatus.Replaced, document.Status); Assert.Equal(DocumentStatus.PendingReview, replacement.Status);
    }

    [Fact]
    public void ZonesShiftsViolationsAndSuspensionsPreserveRules()
    {
        Driver driver = Eligible(); Guid zone = driver.ZoneAssignments.Single().ZoneId; Assert.Throws<DomainException>(() => driver.AssignZone(zone, false, Guid.NewGuid(), Now.AddMinutes(20))); DriverShift shift = driver.ScheduleShift(Now.AddHours(1), Now.AddHours(3), Now.AddMinutes(20)); Assert.Throws<DomainException>(() => driver.ScheduleShift(Now.AddHours(2), Now.AddHours(4), Now.AddMinutes(21))); shift.Start(Now.AddHours(1)); shift.Complete(Now.AddHours(2)); DriverViolation violation = driver.RecordViolation("safety", ViolationSeverity.High, "Unsafe handling", Now, Guid.NewGuid(), Now.AddMinutes(22)); violation.Resolve("Reviewed", Now.AddMinutes(23)); DriverSuspension suspension = driver.Suspend("safety", "Pending investigation", Now.AddMinutes(24), null, Guid.NewGuid(), Now.AddMinutes(24)); Assert.Equal(AvailabilityStatus.Offline, driver.AvailabilityStatus); Assert.Throws<DomainException>(() => driver.GoOnline(Now.AddMinutes(25))); driver.LiftSuspension(suspension.Id, Guid.NewGuid(), "Investigation complete", Now.AddMinutes(26)); Assert.Equal(DriverStatus.Active, driver.Status);
    }

    [Fact]
    public void SuspensionActivityIsDerivedFromTimeAndLiftRatherThanStoredStatus()
    {
        Driver driver = Eligible();
        DriverSuspension future = driver.Suspend("future", "Scheduled", Now.AddHours(2), Now.AddHours(3), Guid.NewGuid(), Now.AddMinutes(10));
        Assert.False(future.IsActiveAt(Now.AddHours(1))); Assert.Equal(DriverStatus.Active, driver.Status); Assert.True(driver.GoOnline(Now.AddHours(1)));
        driver.GoOffline(Now.AddHours(1).AddMinutes(1)); Assert.Throws<DomainException>(() => driver.GoOnline(Now.AddHours(2)));
        Assert.True(driver.GoOnline(Now.AddHours(3)));

        future.Lift(Guid.NewGuid(), "lifted", Now.AddHours(2).AddMinutes(10)); Assert.False(future.IsActiveAt(Now.AddHours(2).AddMinutes(11)));
    }

    [Fact]
    public void OpenFiniteBoundaryAndHistoricalSuspensionsHaveClearSemanticsWithoutWorker()
    {
        Driver ended = Eligible(); DriverSuspension finite = ended.Suspend("finite", "Finite", Now.AddMinutes(10), Now.AddMinutes(20), Guid.NewGuid(), Now.AddMinutes(10));
        Assert.True(finite.IsActiveAt(Now.AddMinutes(10))); Assert.True(finite.IsActiveAt(Now.AddMinutes(19))); Assert.False(finite.IsActiveAt(Now.AddMinutes(20))); Assert.True(ended.GoOnline(Now.AddMinutes(20)));

        Driver open = Eligible(); DriverSuspension indefinite = open.Suspend("open", "Open", Now.AddMinutes(10), null, Guid.NewGuid(), Now.AddMinutes(10)); Assert.True(indefinite.IsActiveAt(Now.AddDays(10))); indefinite.Lift(Guid.NewGuid(), "done", Now.AddMinutes(11)); Assert.True(open.GoOnline(Now.AddMinutes(12)));

        Driver multiple = Eligible(); DriverSuspension historical = multiple.Suspend("old", "Historical", Now.AddMinutes(10), Now.AddMinutes(11), Guid.NewGuid(), Now.AddMinutes(10)); DriverSuspension active = multiple.Suspend("current", "Current", Now.AddMinutes(12), null, Guid.NewGuid(), Now.AddMinutes(12)); Assert.False(historical.IsActiveAt(Now.AddMinutes(13))); Assert.True(active.IsActiveAt(Now.AddMinutes(13))); Assert.Throws<DomainException>(() => multiple.GoOnline(Now.AddMinutes(13)));
    }

    private static Driver Create() => Driver.Create(DriverId.New(), Guid.NewGuid(), "Driver One", EmploymentType.IndependentContractor, 2, null, Now);
    private static Driver Activated()
    {
        Driver driver = Create(); driver.SubmitForReview(Now.AddMinutes(1)); driver.Approve(Now.AddMinutes(2)); driver.Activate(Now.AddMinutes(3)); return driver;
    }
    private static Vehicle AddApprovedPrimaryVehicle(Driver driver)
    {
        Vehicle vehicle = driver.AddVehicle(VehicleType.Car, "Toyota", "Corolla", 2024, "White", Guid.NewGuid().ToString("N")[..8], "IL", true, Now.AddMinutes(4)); vehicle.Approve(Now.AddMinutes(5)); driver.SetPrimaryVehicle(vehicle.Id, Now.AddMinutes(6)); return vehicle;
    }
    private static Driver EligibleWithoutDocuments()
    {
        Driver driver = Activated(); AddApprovedPrimaryVehicle(driver); driver.AssignZone(Guid.NewGuid(), true, Guid.NewGuid(), Now.AddMinutes(6)); return driver;
    }
    private static void AddApprovedDocument(Driver driver, DocumentType type, DateTime expiresAtUtc)
    {
        DriverDocument document = driver.SubmitDocument(type, Guid.NewGuid(), Now.AddDays(-10), expiresAtUtc, Now.AddMinutes(7)); document.Approve(Guid.NewGuid(), Now.AddMinutes(8));
    }
    private static void AddRequiredDocuments(Driver driver)
    {
        foreach (DocumentType type in DriverEligibilityPolicy.RequiredDocuments(VehicleType.Car)) AddApprovedDocument(driver, type, Now.AddDays(100));
    }
    private static Driver Eligible()
    {
        Driver driver = EligibleWithoutDocuments(); AddRequiredDocuments(driver); return driver;
    }
}
