using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Drivers.Domain;

public readonly record struct DriverId { public DriverId(Guid value) { if (value == Guid.Empty) throw new DomainException("Driver identifier is required."); Value = value; } public Guid Value { get; } public static DriverId New() => new(Guid.NewGuid()); }
public readonly record struct VehicleId { public VehicleId(Guid value) { if (value == Guid.Empty) throw new DomainException("Vehicle identifier is required."); Value = value; } public Guid Value { get; } public static VehicleId New() => new(Guid.NewGuid()); }
public readonly record struct DriverDocumentId { public DriverDocumentId(Guid value) { if (value == Guid.Empty) throw new DomainException("Document identifier is required."); Value = value; } public Guid Value { get; } public static DriverDocumentId New() => new(Guid.NewGuid()); }
public readonly record struct DriverZoneAssignmentId { public DriverZoneAssignmentId(Guid value) { if (value == Guid.Empty) throw new DomainException("Zone assignment identifier is required."); Value = value; } public Guid Value { get; } public static DriverZoneAssignmentId New() => new(Guid.NewGuid()); }
public readonly record struct DriverShiftId { public DriverShiftId(Guid value) { if (value == Guid.Empty) throw new DomainException("Shift identifier is required."); Value = value; } public Guid Value { get; } public static DriverShiftId New() => new(Guid.NewGuid()); }
public readonly record struct DriverViolationId { public DriverViolationId(Guid value) { if (value == Guid.Empty) throw new DomainException("Violation identifier is required."); Value = value; } public Guid Value { get; } public static DriverViolationId New() => new(Guid.NewGuid()); }
public readonly record struct DriverSuspensionId { public DriverSuspensionId(Guid value) { if (value == Guid.Empty) throw new DomainException("Suspension identifier is required."); Value = value; } public Guid Value { get; } public static DriverSuspensionId New() => new(Guid.NewGuid()); }
public readonly record struct DriverOutboxMessageId { public DriverOutboxMessageId(Guid value) { if (value == Guid.Empty) throw new DomainException("Outbox identifier is required."); Value = value; } public Guid Value { get; } public static DriverOutboxMessageId New() => new(Guid.NewGuid()); }
public readonly record struct DriverAuditId { public DriverAuditId(Guid value) { if (value == Guid.Empty) throw new DomainException("Audit identifier is required."); Value = value; } public Guid Value { get; } public static DriverAuditId New() => new(Guid.NewGuid()); }
public readonly record struct DriverIdempotencyId { public DriverIdempotencyId(Guid value) { if (value == Guid.Empty) throw new DomainException("Idempotency identifier is required."); Value = value; } public Guid Value { get; } public static DriverIdempotencyId New() => new(Guid.NewGuid()); }

public enum DriverStatus : short { PendingReview = 1, Active = 2, Inactive = 3, Suspended = 4, Blocked = 5, Archived = 6 }
public enum DriverActivationStatus : short { NotSubmitted = 1, PendingReview = 2, RequiresChanges = 3, Approved = 4, Rejected = 5 }
public enum EmploymentType : short { IndependentContractor = 1, Employee = 2, FleetPartner = 3 }
public enum AvailabilityStatus : short { Offline = 1, Online = 2, Busy = 3, OnBreak = 4, Unavailable = 5 }
public enum VehicleType : short { Bicycle = 1, Motorcycle = 2, Car = 3, Van = 4, Truck = 5 }
public enum VehicleStatus : short { PendingVerification = 1, Active = 2, Inactive = 3, Rejected = 4, Expired = 5, Retired = 6 }
public enum DocumentType : short { IdentityDocument = 1, DrivingLicense = 2, VehicleRegistration = 3, VehicleInsurance = 4, BackgroundCheck = 5, WorkPermit = 6, ProfilePhoto = 7, Other = 8 }
public enum DocumentStatus : short { PendingReview = 1, Approved = 2, Rejected = 3, Expired = 4, Replaced = 5 }
public enum DriverShiftStatus : short { Scheduled = 1, Started = 2, Completed = 3, Cancelled = 4, Missed = 5 }
public enum ViolationSeverity : short { Low = 1, Medium = 2, High = 3, Critical = 4 }
public enum DriverViolationStatus : short { Open = 1, Resolved = 2 }
public enum DriverSuspensionStatus : short { Active = 1, Lifted = 2, Expired = 3 }

public static class DriverRules
{
    public const int DisplayNameMaximumLength = 200;
    public const int TextMaximumLength = 500;
    public const int CodeMaximumLength = 80;
    public const int PlateMaximumLength = 32;
    public const int MaximumPageSize = 100;
}
