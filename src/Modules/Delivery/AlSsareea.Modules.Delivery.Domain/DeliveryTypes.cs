using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Delivery.Domain;

public readonly record struct DeliveryId
{
    public DeliveryId(Guid value)
    {
        if (value == Guid.Empty) throw new DomainException("Delivery identifier is required.");
        Value = value;
    }

    public Guid Value { get; }
    public static DeliveryId New() => new(Guid.NewGuid());
}

public readonly record struct DeliveryStatusHistoryId
{
    public DeliveryStatusHistoryId(Guid value)
    {
        if (value == Guid.Empty) throw new DomainException("Delivery history identifier is required.");
        Value = value;
    }

    public Guid Value { get; }
    public static DeliveryStatusHistoryId New() => new(Guid.NewGuid());
}

public readonly record struct DeliveryProofId
{
    public DeliveryProofId(Guid value)
    {
        if (value == Guid.Empty) throw new DomainException("Delivery proof identifier is required.");
        Value = value;
    }

    public Guid Value { get; }
    public static DeliveryProofId New() => new(Guid.NewGuid());
}

public enum DeliveryStatus : short
{
    Created = 1,
    Assigned = 2,
    HeadingToPickup = 3,
    ArrivedAtPickup = 4,
    PickedUp = 5,
    InTransit = 6,
    ArrivedAtDropOff = 7,
    Delivered = 8,
    Failed = 9,
    Cancelled = 10,
}

[Flags]
public enum ProofRequirement : short
{
    None = 0,
    Pin = 1,
    Photo = 2,
    Signature = 4,
    RecipientName = 8,
}

public enum DeliveryProofType : short { Pin = 1, Photo = 2, Signature = 3, RecipientName = 4 }
public enum DeliveryFailureReason : short { RecipientUnavailable = 1, RecipientRefused = 2, InvalidAddress = 3, UnableToAccessLocation = 4, SafetyIssue = 5, VehicleIssue = 6, Other = 7 }
public enum DeliveryChangeSource : short { Dispatch = 1, Driver = 2, Operations = 3, System = 4 }

public static class DeliveryRules
{
    public const int AddressMaximumLength = 500;
    public const int ContactMaximumLength = 100;
    public const int InstructionsMaximumLength = 1000;
    public const int RecipientNameMaximumLength = 200;
    public const int FailureNotesMaximumLength = 500;
    public const int IdempotencyKeyMaximumLength = 200;
    public const int MaximumPinAttempts = 5;
}
