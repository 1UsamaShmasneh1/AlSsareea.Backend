using System.Globalization;
using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Drivers.Domain;

public sealed class Driver : AggregateRoot<DriverId>
{
    private readonly List<Vehicle> _vehicles = [];
    private readonly List<DriverDocument> _documents = [];
    private readonly List<DriverZoneAssignment> _zoneAssignments = [];
    private readonly List<DriverShift> _shifts = [];
    private readonly List<DriverViolation> _violations = [];
    private readonly List<DriverSuspension> _suspensions = [];

    private Driver(DriverId id) : base(id) { }

    private Driver(DriverId id, Guid userId, string displayName, EmploymentType employmentType, int maximumConcurrentDeliveries, Guid? profilePhotoMediaId, DateTime now) : base(id)
    {
        RequireGuid(userId, "User"); RequireUtc(now); ValidateEnum(employmentType, "Employment type");
        UserId = userId; DisplayName = Required(displayName, DriverRules.DisplayNameMaximumLength, "Display name");
        EmploymentType = employmentType; SetCapacity(maximumConcurrentDeliveries, now, false); ProfilePhotoMediaId = profilePhotoMediaId;
        Status = DriverStatus.PendingReview; ActivationStatus = DriverActivationStatus.NotSubmitted; AvailabilityStatus = AvailabilityStatus.Offline;
        CreatedAtUtc = now; UpdatedAtUtc = now; LastAvailabilityChangedAtUtc = now; ConcurrencyStamp = Guid.NewGuid();
        RaiseDomainEvent(new DriverCreatedDomainEvent(id.Value, userId, now));
    }

    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public DateOnly? DateOfBirth { get; private set; }
    public Guid? ProfilePhotoMediaId { get; private set; }
    public DriverStatus Status { get; private set; }
    public DriverActivationStatus ActivationStatus { get; private set; }
    public EmploymentType EmploymentType { get; private set; }
    public AvailabilityStatus AvailabilityStatus { get; private set; }
    public int MaximumConcurrentDeliveries { get; private set; }
    public int CurrentLoad { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public DateTime LastAvailabilityChangedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<Vehicle> Vehicles => _vehicles.AsReadOnly();
    public IReadOnlyCollection<DriverDocument> Documents => _documents.AsReadOnly();
    public IReadOnlyCollection<DriverZoneAssignment> ZoneAssignments => _zoneAssignments.AsReadOnly();
    public IReadOnlyCollection<DriverShift> Shifts => _shifts.AsReadOnly();
    public IReadOnlyCollection<DriverViolation> Violations => _violations.AsReadOnly();
    public IReadOnlyCollection<DriverSuspension> Suspensions => _suspensions.AsReadOnly();

    public static Driver Create(DriverId id, Guid userId, string displayName, EmploymentType employmentType, int maximumConcurrentDeliveries, Guid? profilePhotoMediaId, DateTime now) => new(id, userId, displayName, employmentType, maximumConcurrentDeliveries, profilePhotoMediaId, now);

    public void UpdateProfile(string displayName, DateOnly? dateOfBirth, Guid? profilePhotoMediaId, DateTime now)
    {
        RequireUtc(now); DisplayName = Required(displayName, DriverRules.DisplayNameMaximumLength, "Display name");
        if (dateOfBirth.HasValue && dateOfBirth >= DateOnly.FromDateTime(now)) throw new DomainException("Date of birth must be in the past.");
        DateOfBirth = dateOfBirth; ProfilePhotoMediaId = profilePhotoMediaId; Touch(now);
    }

    public void SubmitForReview(DateTime now)
    {
        RequireUtc(now); if (ActivationStatus is not (DriverActivationStatus.NotSubmitted or DriverActivationStatus.RequiresChanges or DriverActivationStatus.Rejected)) throw new DomainException("Driver cannot be submitted in the current state.");
        ActivationStatus = DriverActivationStatus.PendingReview; Status = DriverStatus.PendingReview; Touch(now); RaiseDomainEvent(new DriverActivationChangedDomainEvent(Id.Value, ActivationStatus, now));
    }

    public void Approve(DateTime now)
    {
        RequireUtc(now); if (ActivationStatus != DriverActivationStatus.PendingReview) throw new DomainException("Only a pending driver can be approved.");
        ActivationStatus = DriverActivationStatus.Approved; Touch(now); RaiseDomainEvent(new DriverActivationChangedDomainEvent(Id.Value, ActivationStatus, now));
    }

    public void Reject(DateTime now)
    {
        RequireUtc(now); if (ActivationStatus != DriverActivationStatus.PendingReview) throw new DomainException("Only a pending driver can be rejected.");
        ActivationStatus = DriverActivationStatus.Rejected; Status = DriverStatus.Inactive; AvailabilityStatus = AvailabilityStatus.Offline; TouchAvailability(now); RaiseDomainEvent(new DriverActivationChangedDomainEvent(Id.Value, ActivationStatus, now));
    }

    public void Activate(DateTime now)
    {
        RequireUtc(now); if (ActivationStatus != DriverActivationStatus.Approved) throw new DomainException("Driver approval is required before activation.");
        if (HasActiveSuspension(now)) throw new DomainException("A suspended driver cannot be activated.");
        Status = DriverStatus.Active; ActivatedAtUtc = now; Touch(now);
    }

    public void Deactivate(DateTime now)
    {
        RequireUtc(now); Status = DriverStatus.Inactive; AvailabilityStatus = AvailabilityStatus.Offline; TouchAvailability(now);
    }

    public void Archive(DateTime now)
    {
        RequireUtc(now); if (CurrentLoad != 0) throw new DomainException("A driver with current load cannot be archived.");
        Status = DriverStatus.Archived; AvailabilityStatus = AvailabilityStatus.Offline; ArchivedAtUtc = now; TouchAvailability(now);
    }

    public bool GoOnline(DateTime now)
    {
        RequireUtc(now); if (AvailabilityStatus == AvailabilityStatus.Online) return false; DriverEligibilityPolicy.EnsureCanGoOnline(this, now); ChangeAvailability(AvailabilityStatus.Online, now); return true;
    }

    public bool GoOffline(DateTime now)
    {
        RequireUtc(now); if (AvailabilityStatus == AvailabilityStatus.Offline) return false; ChangeAvailability(AvailabilityStatus.Offline, now); return true;
    }

    public void StartBreak(DateTime now)
    {
        RequireUtc(now); if (AvailabilityStatus is not (AvailabilityStatus.Online or AvailabilityStatus.Busy)) throw new DomainException("Break can start only while online."); ChangeAvailability(AvailabilityStatus.OnBreak, now);
    }

    public void EndBreak(DateTime now)
    {
        RequireUtc(now); if (AvailabilityStatus != AvailabilityStatus.OnBreak) throw new DomainException("Driver is not on a break."); DriverEligibilityPolicy.EnsureCanGoOnline(this, now); ChangeAvailability(CurrentLoad == 0 ? AvailabilityStatus.Online : AvailabilityStatus.Busy, now);
    }

    public void UpdateCurrentLoad(int currentLoad, DateTime now)
    {
        RequireUtc(now); if (currentLoad < 0 || currentLoad > MaximumConcurrentDeliveries) throw new DomainException("Current load is outside driver capacity.");
        CurrentLoad = currentLoad; if (AvailabilityStatus is AvailabilityStatus.Online or AvailabilityStatus.Busy) ChangeAvailability(currentLoad == 0 ? AvailabilityStatus.Online : AvailabilityStatus.Busy, now); else Touch(now);
    }

    public void ChangeCapacity(int maximum, DateTime now) => SetCapacity(maximum, now, true);

    public Vehicle AddVehicle(VehicleType type, string? make, string? model, int? year, string? color, string? plateNumber, string? registrationCountry, bool isPrimary, DateTime now)
    {
        RequireUtc(now); if (isPrimary && _vehicles.Any(x => x.IsPrimary && x.Status != VehicleStatus.Retired)) throw new DomainException("Driver already has a primary vehicle.");
        Vehicle vehicle = global::AlSsareea.Modules.Drivers.Domain.Vehicle.Create(VehicleId.New(), Id, type, make, model, year, color, plateNumber, registrationCountry, isPrimary, now);
        if (_vehicles.Any(x => x.NormalizedPlateNumber is not null && x.NormalizedPlateNumber == vehicle.NormalizedPlateNumber && x.Status != VehicleStatus.Retired)) throw new DomainException("Vehicle plate is already registered for this driver.");
        _vehicles.Add(vehicle); Touch(now); RaiseDomainEvent(new DriverVehicleChangedDomainEvent(Id.Value, vehicle.Id.Value, now)); return vehicle;
    }

    public void SetPrimaryVehicle(VehicleId vehicleId, DateTime now)
    {
        RequireUtc(now); Vehicle target = Vehicle(vehicleId); if (target.Status != VehicleStatus.Active) throw new DomainException("Only an approved active vehicle can be primary.");
        foreach (Vehicle vehicle in _vehicles) vehicle.SetPrimary(vehicle == target, now); Touch(now); RaiseDomainEvent(new DriverVehicleChangedDomainEvent(Id.Value, target.Id.Value, now));
    }

    public DriverDocument SubmitDocument(DocumentType type, Guid mediaAssetId, DateTime? issuedAtUtc, DateTime? expiresAtUtc, DateTime now)
    {
        RequireUtc(now); RequireGuid(mediaAssetId, "Media asset");
        DriverDocument? existing = _documents.SingleOrDefault(x => x.Type == type && x.Status is DocumentStatus.PendingReview or DocumentStatus.Approved);
        if (existing is not null) existing.Replace(now);
        DriverDocument document = DriverDocument.Create(DriverDocumentId.New(), Id, type, mediaAssetId, issuedAtUtc, expiresAtUtc, now); _documents.Add(document); Touch(now); return document;
    }

    public DriverZoneAssignment AssignZone(Guid zoneId, bool primary, Guid actorUserId, DateTime now)
    {
        RequireUtc(now); RequireGuid(zoneId, "Zone"); RequireGuid(actorUserId, "Actor");
        if (_zoneAssignments.Any(x => x.ZoneId == zoneId && x.IsActive)) throw new DomainException("Zone is already assigned.");
        if (primary) foreach (DriverZoneAssignment zone in _zoneAssignments.Where(x => x.IsActive)) zone.SetPrimary(false);
        DriverZoneAssignment assignment = DriverZoneAssignment.Create(DriverZoneAssignmentId.New(), Id, zoneId, primary, actorUserId, now); _zoneAssignments.Add(assignment); Touch(now); return assignment;
    }

    public void RemoveZone(Guid zoneId, DateTime now)
    {
        RequireUtc(now); DriverZoneAssignment assignment = _zoneAssignments.SingleOrDefault(x => x.ZoneId == zoneId && x.IsActive) ?? throw new DomainException("Active zone assignment was not found.");
        assignment.Remove(now); Touch(now);
    }

    public DriverShift ScheduleShift(DateTime startUtc, DateTime endUtc, DateTime now)
    {
        RequireUtc(now); RequireUtc(startUtc); RequireUtc(endUtc); if (endUtc <= startUtc) throw new DomainException("Shift end must be after start.");
        if (_shifts.Any(x => x.Status is DriverShiftStatus.Scheduled or DriverShiftStatus.Started && startUtc < x.ScheduledEndUtc && endUtc > x.ScheduledStartUtc)) throw new DomainException("Driver shifts cannot overlap.");
        DriverShift shift = DriverShift.Create(DriverShiftId.New(), Id, startUtc, endUtc, now); _shifts.Add(shift); Touch(now); return shift;
    }

    public DriverViolation RecordViolation(string type, ViolationSeverity severity, string description, DateTime occurredAtUtc, Guid actorUserId, DateTime now)
    {
        DriverViolation violation = DriverViolation.Create(DriverViolationId.New(), Id, type, severity, description, occurredAtUtc, actorUserId, now); _violations.Add(violation); Touch(now); return violation;
    }

    public DriverSuspension Suspend(string reasonCode, string reason, DateTime startsAtUtc, DateTime? endsAtUtc, Guid actorUserId, DateTime now)
    {
        RequireUtc(now); RequireUtc(startsAtUtc); if (endsAtUtc.HasValue) RequireUtc(endsAtUtc.Value);
        if (_suspensions.Any(x => x.Overlaps(startsAtUtc, endsAtUtc))) throw new DomainException("Driver already has an overlapping suspension.");
        DriverSuspension suspension = DriverSuspension.Create(DriverSuspensionId.New(), Id, reasonCode, reason, startsAtUtc, endsAtUtc, actorUserId, now); _suspensions.Add(suspension);
        if (suspension.IsActiveAt(now))
        {
            Status = DriverStatus.Suspended; AvailabilityStatus = AvailabilityStatus.Offline; SuspendedAtUtc = now; TouchAvailability(now);
            RaiseDomainEvent(new DriverSuspendedDomainEvent(Id.Value, suspension.Id.Value, now));
        }
        else Touch(now);
        return suspension;
    }

    public void LiftSuspension(DriverSuspensionId suspensionId, Guid actorUserId, string reason, DateTime now)
    {
        DriverSuspension suspension = _suspensions.SingleOrDefault(x => x.Id == suspensionId) ?? throw new DomainException("Suspension was not found."); suspension.Lift(actorUserId, reason, now);
        if (!HasActiveSuspension(now)) { Status = ActivationStatus == DriverActivationStatus.Approved ? DriverStatus.Active : DriverStatus.Inactive; SuspendedAtUtc = null; }
        Touch(now); RaiseDomainEvent(new DriverSuspensionLiftedDomainEvent(Id.Value, suspension.Id.Value, now));
    }

    public bool HasActiveSuspension(DateTime atUtc) => _suspensions.Any(x => x.IsActiveAt(atUtc));
    public bool IsOperationallyActiveAt(DateTime atUtc)
    {
        RequireUtc(atUtc);
        return Status == DriverStatus.Active || Status == DriverStatus.Suspended && !HasActiveSuspension(atUtc);
    }
    public Vehicle Vehicle(VehicleId id) => _vehicles.SingleOrDefault(x => x.Id == id) ?? throw new DomainException("Vehicle was not found.");
    public DriverDocument Document(DriverDocumentId id) => _documents.SingleOrDefault(x => x.Id == id) ?? throw new DomainException("Document was not found.");
    public DriverShift Shift(DriverShiftId id) => _shifts.SingleOrDefault(x => x.Id == id) ?? throw new DomainException("Shift was not found.");
    public DriverViolation Violation(DriverViolationId id) => _violations.SingleOrDefault(x => x.Id == id) ?? throw new DomainException("Violation was not found.");

    private void ChangeAvailability(AvailabilityStatus next, DateTime now)
    {
        AvailabilityStatus previous = AvailabilityStatus; if (previous == next) return; AvailabilityStatus = next; TouchAvailability(now); RaiseDomainEvent(new DriverAvailabilityChangedDomainEvent(Id.Value, previous, next, now));
    }
    private void SetCapacity(int maximum, DateTime now, bool touch) { if (maximum <= 0 || maximum > 20 || CurrentLoad > maximum) throw new DomainException("Driver capacity is invalid."); MaximumConcurrentDeliveries = maximum; if (touch) Touch(now); }
    private void TouchAvailability(DateTime now) { LastAvailabilityChangedAtUtc = now; Touch(now); }
    private void Touch(DateTime now) { RequireUtc(now); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    private static string Required(string value, int max, string name) { string normalized = value?.Trim() ?? string.Empty; if (normalized.Length == 0 || normalized.Length > max) throw new DomainException($"{name} is invalid."); return normalized; }
    internal static void RequireUtc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new DomainException("UTC timestamp is required."); }
    internal static void RequireGuid(Guid value, string name) { if (value == Guid.Empty) throw new DomainException($"{name} identifier is required."); }
    internal static void ValidateEnum<T>(T value, string name) where T : struct, Enum { if (!Enum.IsDefined(value) || Convert.ToInt16(value, CultureInfo.InvariantCulture) == 0) throw new DomainException($"{name} is invalid."); }
}

public sealed class Vehicle : Entity<VehicleId>
{
    private Vehicle(VehicleId id) : base(id) { }
    private Vehicle(VehicleId id, DriverId driverId, VehicleType type, string? make, string? model, int? year, string? color, string? plate, string? country, bool primary, DateTime now) : base(id)
    {
        Driver.ValidateEnum(type, "Vehicle type"); Driver.RequireUtc(now); if (year is < 1980 or > 2100) throw new DomainException("Vehicle year is invalid.");
        string? normalizedPlate = NormalizePlate(plate); if (type != VehicleType.Bicycle && normalizedPlate is null) throw new DomainException("Vehicle plate is required.");
        DriverId = driverId; Type = type; Make = Trim(make, 100); Model = Trim(model, 100); Year = year; Color = Trim(color, 50); PlateNumber = Trim(plate, DriverRules.PlateMaximumLength); NormalizedPlateNumber = normalizedPlate; RegistrationCountry = Trim(country, 2)?.ToUpperInvariant(); IsPrimary = primary; Status = VehicleStatus.PendingVerification; CreatedAtUtc = now; UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid();
    }
    public DriverId DriverId { get; private set; }
    public VehicleType Type { get; private set; }
    public string? Make { get; private set; }
    public string? Model { get; private set; }
    public int? Year { get; private set; }
    public string? Color { get; private set; }
    public string? PlateNumber { get; private set; }
    public string? NormalizedPlateNumber { get; private set; }
    public string? RegistrationCountry { get; private set; }
    public bool IsPrimary { get; private set; }
    public VehicleStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    internal static Vehicle Create(VehicleId id, DriverId driverId, VehicleType type, string? make, string? model, int? year, string? color, string? plate, string? country, bool primary, DateTime now) => new(id, driverId, type, make, model, year, color, plate, country, primary, now);
    public void Approve(DateTime now) { Driver.RequireUtc(now); if (Status != VehicleStatus.PendingVerification) throw new DomainException("Vehicle is not pending verification."); Status = VehicleStatus.Active; VerifiedAtUtc = now; Touch(now); }
    public void Reject(DateTime now) { Driver.RequireUtc(now); if (Status != VehicleStatus.PendingVerification) throw new DomainException("Vehicle is not pending verification."); Status = VehicleStatus.Rejected; IsPrimary = false; Touch(now); }
    public void Deactivate(DateTime now) { Driver.RequireUtc(now); Status = VehicleStatus.Inactive; IsPrimary = false; Touch(now); }
    internal void SetPrimary(bool value, DateTime now) { if (value && Status != VehicleStatus.Active) throw new DomainException("Only active vehicles can be primary."); IsPrimary = value; Touch(now); }
    private void Touch(DateTime now) { UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    private static string? NormalizePlate(string? value) { string? text = Trim(value, DriverRules.PlateMaximumLength); return text is null ? null : string.Concat(text.Where(char.IsLetterOrDigit)).ToUpperInvariant(); }
    private static string? Trim(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; string text = value.Trim(); if (text.Length > max) throw new DomainException("Vehicle value is too long."); return text; }
}

public sealed class DriverDocument : Entity<DriverDocumentId>
{
    private DriverDocument(DriverDocumentId id) : base(id) { }
    private DriverDocument(DriverDocumentId id, DriverId driverId, DocumentType type, Guid mediaAssetId, DateTime? issued, DateTime? expires, DateTime now) : base(id)
    {
        Driver.ValidateEnum(type, "Document type"); Driver.RequireGuid(mediaAssetId, "Media asset"); Driver.RequireUtc(now); if (issued.HasValue) Driver.RequireUtc(issued.Value); if (expires.HasValue) Driver.RequireUtc(expires.Value); if (issued.HasValue && expires <= issued) throw new DomainException("Document expiry must be after issue date.");
        DriverId = driverId; Type = type; MediaAssetId = mediaAssetId; Status = DocumentStatus.PendingReview; IssuedAtUtc = issued; ExpiresAtUtc = expires; SubmittedAtUtc = now; CreatedAtUtc = now; UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid();
    }
    public DriverId DriverId { get; private set; }
    public DocumentType Type { get; private set; }
    public Guid MediaAssetId { get; private set; }
    public DocumentStatus Status { get; private set; }
    public DateTime? IssuedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    internal static DriverDocument Create(DriverDocumentId id, DriverId driverId, DocumentType type, Guid mediaAssetId, DateTime? issued, DateTime? expires, DateTime now) => new(id, driverId, type, mediaAssetId, issued, expires, now);
    public void Approve(Guid reviewer, DateTime now) { Driver.RequireGuid(reviewer, "Reviewer"); Driver.RequireUtc(now); if (Status != DocumentStatus.PendingReview || ExpiresAtUtc <= now) throw new DomainException("Document cannot be approved."); Status = DocumentStatus.Approved; ReviewedByUserId = reviewer; ReviewedAtUtc = now; RejectionReason = null; Touch(now); }
    public void Reject(Guid reviewer, string reason, DateTime now) { Driver.RequireGuid(reviewer, "Reviewer"); Driver.RequireUtc(now); if (Status != DocumentStatus.PendingReview || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > DriverRules.TextMaximumLength) throw new DomainException("Document rejection is invalid."); Status = DocumentStatus.Rejected; ReviewedByUserId = reviewer; ReviewedAtUtc = now; RejectionReason = reason.Trim(); Touch(now); }
    internal void Replace(DateTime now) { if (Status is DocumentStatus.Approved or DocumentStatus.PendingReview) { Status = DocumentStatus.Replaced; Touch(now); } }
    private void Touch(DateTime now) { UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
}

public sealed class DriverZoneAssignment : Entity<DriverZoneAssignmentId>
{
    private DriverZoneAssignment(DriverZoneAssignmentId id) : base(id) { }
    private DriverZoneAssignment(DriverZoneAssignmentId id, DriverId driverId, Guid zoneId, bool primary, Guid actor, DateTime now) : base(id) { DriverId = driverId; ZoneId = zoneId; IsPrimary = primary; IsActive = true; AssignedAtUtc = now; AssignedByUserId = actor; }
    public DriverId DriverId { get; private set; }
    public Guid ZoneId { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
    public Guid AssignedByUserId { get; private set; }
    public DateTime? RemovedAtUtc { get; private set; }
    internal static DriverZoneAssignment Create(DriverZoneAssignmentId id, DriverId driverId, Guid zoneId, bool primary, Guid actor, DateTime now) => new(id, driverId, zoneId, primary, actor, now);
    internal void SetPrimary(bool value) => IsPrimary = value;
    internal void Remove(DateTime now) { Driver.RequireUtc(now); IsActive = false; IsPrimary = false; RemovedAtUtc = now; }
}

public sealed class DriverShift : Entity<DriverShiftId>
{
    private DriverShift(DriverShiftId id) : base(id) { }
    private DriverShift(DriverShiftId id, DriverId driverId, DateTime start, DateTime end, DateTime now) : base(id) { DriverId = driverId; ScheduledStartUtc = start; ScheduledEndUtc = end; Status = DriverShiftStatus.Scheduled; CreatedAtUtc = now; UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    public DriverId DriverId { get; private set; }
    public DateTime ScheduledStartUtc { get; private set; }
    public DateTime ScheduledEndUtc { get; private set; }
    public DateTime? ActualStartUtc { get; private set; }
    public DateTime? ActualEndUtc { get; private set; }
    public DriverShiftStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    internal static DriverShift Create(DriverShiftId id, DriverId driverId, DateTime start, DateTime end, DateTime now) => new(id, driverId, start, end, now);
    public void Start(DateTime now) { Driver.RequireUtc(now); if (Status != DriverShiftStatus.Scheduled) throw new DomainException("Shift cannot be started."); Status = DriverShiftStatus.Started; ActualStartUtc = now; Touch(now); }
    public void Complete(DateTime now) { Driver.RequireUtc(now); if (Status != DriverShiftStatus.Started || now < ActualStartUtc) throw new DomainException("Shift cannot be completed."); Status = DriverShiftStatus.Completed; ActualEndUtc = now; Touch(now); }
    public void Cancel(DateTime now) { Driver.RequireUtc(now); if (Status != DriverShiftStatus.Scheduled) throw new DomainException("Only scheduled shifts can be cancelled."); Status = DriverShiftStatus.Cancelled; Touch(now); }
    private void Touch(DateTime now) { UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
}

public sealed class DriverViolation : Entity<DriverViolationId>
{
    private DriverViolation(DriverViolationId id) : base(id) { }
    private DriverViolation(DriverViolationId id, DriverId driverId, string type, ViolationSeverity severity, string description, DateTime occurred, Guid actor, DateTime now) : base(id) { Driver.RequireUtc(occurred); Driver.RequireUtc(now); Driver.RequireGuid(actor, "Actor"); Driver.ValidateEnum(severity, "Violation severity"); ViolationType = Required(type, DriverRules.CodeMaximumLength); Description = Required(description, DriverRules.TextMaximumLength); DriverId = driverId; Severity = severity; OccurredAtUtc = occurred; RecordedAtUtc = now; RecordedByUserId = actor; Status = DriverViolationStatus.Open; }
    public DriverId DriverId { get; private set; }
    public string ViolationType { get; private set; } = string.Empty; public ViolationSeverity Severity { get; private set; }
    public string Description { get; private set; } = string.Empty; public DateTime OccurredAtUtc { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DriverViolationStatus Status { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public string? ResolutionNotes { get; private set; }
    internal static DriverViolation Create(DriverViolationId id, DriverId driverId, string type, ViolationSeverity severity, string description, DateTime occurred, Guid actor, DateTime now) => new(id, driverId, type, severity, description, occurred, actor, now);
    public void Resolve(string notes, DateTime now) { Driver.RequireUtc(now); if (Status == DriverViolationStatus.Resolved) return; ResolutionNotes = Required(notes, DriverRules.TextMaximumLength); Status = DriverViolationStatus.Resolved; ResolvedAtUtc = now; }
    private static string Required(string value, int max) { string text = value?.Trim() ?? string.Empty; if (text.Length == 0 || text.Length > max) throw new DomainException("Violation value is invalid."); return text; }
}

public sealed class DriverSuspension : Entity<DriverSuspensionId>
{
    private DriverSuspension(DriverSuspensionId id) : base(id) { }
    private DriverSuspension(DriverSuspensionId id, DriverId driverId, string code, string reason, DateTime starts, DateTime? ends, Guid actor, DateTime now) : base(id) { Driver.RequireUtc(starts); Driver.RequireUtc(now); if (ends.HasValue) Driver.RequireUtc(ends.Value); if (ends <= starts) throw new DomainException("Suspension end must be after start."); Driver.RequireGuid(actor, "Actor"); ReasonCode = Required(code, DriverRules.CodeMaximumLength); Reason = Required(reason, DriverRules.TextMaximumLength); DriverId = driverId; StartsAtUtc = starts; EndsAtUtc = ends; CreatedAtUtc = now; CreatedByUserId = actor; Status = DriverSuspensionStatus.Active; }
    public DriverId DriverId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty; public string Reason { get; private set; } = string.Empty; public DateTime StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? LiftedAtUtc { get; private set; }
    public Guid? LiftedByUserId { get; private set; }
    public string? LiftReason { get; private set; }
    public DriverSuspensionStatus Status { get; private set; }
    internal static DriverSuspension Create(DriverSuspensionId id, DriverId driverId, string code, string reason, DateTime starts, DateTime? ends, Guid actor, DateTime now) => new(id, driverId, code, reason, starts, ends, actor, now);
    public bool IsActiveAt(DateTime at) { Driver.RequireUtc(at); return LiftedAtUtc is null && StartsAtUtc <= at && (!EndsAtUtc.HasValue || EndsAtUtc > at); }
    internal bool Overlaps(DateTime startsAtUtc, DateTime? endsAtUtc) => LiftedAtUtc is null && StartsAtUtc < (endsAtUtc ?? DateTime.MaxValue) && startsAtUtc < (EndsAtUtc ?? DateTime.MaxValue);
    public void Lift(Guid actor, string reason, DateTime now) { Driver.RequireGuid(actor, "Actor"); Driver.RequireUtc(now); if (Status == DriverSuspensionStatus.Lifted) return; LiftReason = Required(reason, DriverRules.TextMaximumLength); LiftedByUserId = actor; LiftedAtUtc = now; Status = DriverSuspensionStatus.Lifted; }
    private static string Required(string value, int max) { string text = value?.Trim() ?? string.Empty; if (text.Length == 0 || text.Length > max) throw new DomainException("Suspension value is invalid."); return text; }
}
