using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Delivery.Domain;

public sealed class Delivery : AggregateRoot<DeliveryId>
{
    private readonly List<DeliveryStatusHistory> _statusHistory = [];
    private readonly List<DeliveryProof> _proofs = [];

    private Delivery() : base(default) { }

    private Delivery(DeliveryId id, Guid orderId, Guid customerId, Guid customerUserId, PickupSnapshot pickup, DropOffSnapshot dropOff, ProofRequirement proofRequirements, string? pinHash, string? pinSalt, DateTime createdAtUtc)
        : base(id)
    {
        if (orderId == Guid.Empty || customerId == Guid.Empty || customerUserId == Guid.Empty) throw new DomainException("Delivery references are required.");
        RequireUtc(createdAtUtc);
        if ((proofRequirements & ProofRequirement.Pin) != 0 && (string.IsNullOrWhiteSpace(pinHash) || string.IsNullOrWhiteSpace(pinSalt))) throw new DomainException("PIN proof configuration is required.");
        if ((proofRequirements & ~AllProofRequirements) != 0) throw new DomainException("Proof requirements are invalid.");
        OrderId = orderId;
        CustomerId = customerId;
        CustomerUserId = customerUserId;
        MerchantId = pickup.MerchantId;
        BranchId = pickup.BranchId;
        Pickup = pickup;
        DropOff = dropOff;
        ProofRequirements = proofRequirements;
        PinHash = pinHash;
        PinSalt = pinSalt;
        Status = DeliveryStatus.Created;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        ConcurrencyStamp = Guid.NewGuid();
        AddHistory(null, Status, DeliveryChangeSource.System, createdAtUtc, null, null);
        RaiseDomainEvent(new DeliveryCreatedDomainEvent(id.Value, orderId, customerId, createdAtUtc));
    }

    private const ProofRequirement AllProofRequirements = ProofRequirement.Pin | ProofRequirement.Photo | ProofRequirement.Signature | ProofRequirement.RecipientName;

    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid CustomerUserId { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? DriverId { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public PickupSnapshot Pickup { get; private set; } = null!;
    public DropOffSnapshot DropOff { get; private set; } = null!;
    public ProofRequirement ProofRequirements { get; private set; }
    public string? PinHash { get; private set; }
    public string? PinSalt { get; private set; }
    public int PinFailedAttempts { get; private set; }
    public bool PinLocked { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? AssignedAtUtc { get; private set; }
    public DateTime? HeadingToPickupAtUtc { get; private set; }
    public DateTime? ArrivedAtPickupAtUtc { get; private set; }
    public DateTime? PickedUpAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? ArrivedAtDropOffAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public DeliveryFailureReason? FailureReason { get; private set; }
    public string? FailureNotes { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<DeliveryStatusHistory> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyCollection<DeliveryProof> Proofs => _proofs.AsReadOnly();

    public static Delivery Create(DeliveryId id, Guid orderId, Guid customerId, Guid customerUserId, PickupSnapshot pickup, DropOffSnapshot dropOff, ProofRequirement proofRequirements, string? pinHash, string? pinSalt, DateTime createdAtUtc) =>
        new(id, orderId, customerId, customerUserId, pickup, dropOff, proofRequirements, pinHash, pinSalt, createdAtUtc);

    public void Assign(Guid driverId, DateTime atUtc)
    {
        if (driverId == Guid.Empty) throw new DomainException("Driver identifier is required.");
        EnsureStatus(DeliveryStatus.Created);
        DriverId = driverId;
        AssignedAtUtc = atUtc;
        Transition(DeliveryStatus.Assigned, DeliveryChangeSource.Dispatch, atUtc);
        RaiseDomainEvent(new DriverAssignedToDeliveryDomainEvent(Id.Value, OrderId, driverId, atUtc));
    }

    public void BeginHeadingToPickup(DateTime atUtc)
    {
        EnsureAssigned();
        EnsureStatus(DeliveryStatus.Assigned);
        HeadingToPickupAtUtc = atUtc;
        Transition(DeliveryStatus.HeadingToPickup, DeliveryChangeSource.Driver, atUtc);
    }

    public void ArriveAtPickup(DateTime atUtc)
    {
        EnsureAssigned();
        EnsureStatus(DeliveryStatus.HeadingToPickup);
        ArrivedAtPickupAtUtc = atUtc;
        Transition(DeliveryStatus.ArrivedAtPickup, DeliveryChangeSource.Driver, atUtc);
    }

    public void ConfirmPickup(DateTime atUtc)
    {
        EnsureAssigned();
        EnsureStatus(DeliveryStatus.ArrivedAtPickup);
        PickedUpAtUtc = atUtc;
        Transition(DeliveryStatus.PickedUp, DeliveryChangeSource.Driver, atUtc);
    }

    public void Start(DateTime atUtc)
    {
        EnsureAssigned();
        EnsureStatus(DeliveryStatus.PickedUp);
        StartedAtUtc = atUtc;
        Transition(DeliveryStatus.InTransit, DeliveryChangeSource.Driver, atUtc);
    }

    public void ArriveAtDropOff(DateTime atUtc)
    {
        EnsureAssigned();
        EnsureStatus(DeliveryStatus.InTransit);
        ArrivedAtDropOffAtUtc = atUtc;
        Transition(DeliveryStatus.ArrivedAtDropOff, DeliveryChangeSource.Driver, atUtc);
    }

    public void RecordPinAttempt(bool valid, DateTime atUtc)
    {
        EnsureAssigned();
        EnsureStatus(DeliveryStatus.ArrivedAtDropOff);
        if ((ProofRequirements & ProofRequirement.Pin) == 0) throw new DomainException("PIN proof is not required.");
        if (HasProof(DeliveryProofType.Pin)) return;
        if (PinLocked) throw new DomainException("PIN proof is locked.");
        if (!valid)
        {
            PinFailedAttempts++;
            PinLocked = PinFailedAttempts >= DeliveryRules.MaximumPinAttempts;
            Touch(atUtc);
            return;
        }

        _proofs.Add(DeliveryProof.Pin(DeliveryProofId.New(), Id, DriverId!.Value, atUtc));
        Touch(atUtc);
    }

    public void AddMediaProof(DeliveryProofType type, Guid mediaAssetId, DateTime atUtc)
    {
        EnsureAssigned();
        EnsureStatus(DeliveryStatus.ArrivedAtDropOff);
        ProofRequirement requirement = type switch { DeliveryProofType.Photo => ProofRequirement.Photo, DeliveryProofType.Signature => ProofRequirement.Signature, _ => throw new DomainException("Media proof type is invalid.") };
        if ((ProofRequirements & requirement) == 0) throw new DomainException("Proof type is not required.");
        if (HasProof(type)) return;
        _proofs.Add(DeliveryProof.Media(DeliveryProofId.New(), Id, DriverId!.Value, type, mediaAssetId, atUtc));
        Touch(atUtc);
    }

    public void AddRecipientName(string recipientName, DateTime atUtc)
    {
        EnsureAssigned();
        EnsureStatus(DeliveryStatus.ArrivedAtDropOff);
        if ((ProofRequirements & ProofRequirement.RecipientName) == 0) throw new DomainException("Recipient-name proof is not required.");
        if (HasProof(DeliveryProofType.RecipientName)) return;
        string normalized = PickupSnapshot.Required(recipientName, DeliveryRules.RecipientNameMaximumLength, "Recipient name");
        _proofs.Add(DeliveryProof.Recipient(DeliveryProofId.New(), Id, DriverId!.Value, normalized, atUtc));
        Touch(atUtc);
    }

    public void Complete(DateTime atUtc)
    {
        EnsureAssigned();
        EnsureStatus(DeliveryStatus.ArrivedAtDropOff);
        if (!ProofRequirementsSatisfied()) throw new DomainException("Required proof of delivery is incomplete.");
        DeliveredAtUtc = atUtc;
        Transition(DeliveryStatus.Delivered, DeliveryChangeSource.Driver, atUtc);
        RaiseDomainEvent(new DeliveryCompletedDomainEvent(Id.Value, OrderId, DriverId!.Value, atUtc));
    }

    public void Fail(DeliveryFailureReason reason, string? notes, DateTime atUtc)
    {
        EnsureAssigned();
        if (!Enum.IsDefined(reason)) throw new DomainException("Failure reason is invalid.");
        if (IsTerminal) throw new DomainException("A terminal delivery cannot fail.");
        FailureReason = reason;
        FailureNotes = PickupSnapshot.Optional(notes, DeliveryRules.FailureNotesMaximumLength, "Failure notes");
        FailedAtUtc = atUtc;
        Transition(DeliveryStatus.Failed, DeliveryChangeSource.Driver, atUtc, reason.ToString(), FailureNotes);
        RaiseDomainEvent(new DeliveryFailedDomainEvent(Id.Value, OrderId, DriverId!.Value, reason, atUtc));
    }

    public void Cancel(string? reason, DateTime atUtc)
    {
        if (IsTerminal) throw new DomainException("A terminal delivery cannot be cancelled.");
        CancelledAtUtc = atUtc;
        Transition(DeliveryStatus.Cancelled, DeliveryChangeSource.Operations, atUtc, "cancelled", PickupSnapshot.Optional(reason, DeliveryRules.FailureNotesMaximumLength, "Cancellation reason"));
    }

    public bool IsTerminal => Status is DeliveryStatus.Delivered or DeliveryStatus.Failed or DeliveryStatus.Cancelled;
    public bool IsCustomerTrackingVisible => DriverId.HasValue && Status is DeliveryStatus.PickedUp or DeliveryStatus.InTransit or DeliveryStatus.ArrivedAtDropOff;

    private bool ProofRequirementsSatisfied() =>
        ((ProofRequirements & ProofRequirement.Pin) == 0 || HasProof(DeliveryProofType.Pin)) &&
        ((ProofRequirements & ProofRequirement.Photo) == 0 || HasProof(DeliveryProofType.Photo)) &&
        ((ProofRequirements & ProofRequirement.Signature) == 0 || HasProof(DeliveryProofType.Signature)) &&
        ((ProofRequirements & ProofRequirement.RecipientName) == 0 || HasProof(DeliveryProofType.RecipientName));

    private bool HasProof(DeliveryProofType type) => _proofs.Any(x => x.Type == type);
    private void EnsureAssigned() { if (!DriverId.HasValue) throw new DomainException("Delivery must have an assigned driver."); }
    private void EnsureStatus(DeliveryStatus required) { if (Status != required) throw new DomainException($"Delivery must be in {required} status."); }

    private void Transition(DeliveryStatus next, DeliveryChangeSource source, DateTime atUtc, string? reasonCode = null, string? reasonText = null)
    {
        RequireUtc(atUtc);
        DeliveryStatus previous = Status;
        Status = next;
        AddHistory(previous, next, source, atUtc, reasonCode, reasonText);
        Touch(atUtc);
        RaiseDomainEvent(new DeliveryStatusChangedDomainEvent(Id.Value, previous, next, atUtc));
    }

    private void AddHistory(DeliveryStatus? previous, DeliveryStatus next, DeliveryChangeSource source, DateTime atUtc, string? reasonCode, string? reasonText) =>
        _statusHistory.Add(DeliveryStatusHistory.Create(DeliveryStatusHistoryId.New(), Id, previous, next, source, atUtc, reasonCode, reasonText));

    private void Touch(DateTime atUtc)
    {
        RequireUtc(atUtc);
        UpdatedAtUtc = atUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    private static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new DomainException("Timestamp must be UTC.");
    }
}

public sealed class DeliveryStatusHistory : Entity<DeliveryStatusHistoryId>
{
    private DeliveryStatusHistory() : base(default) { }
    private DeliveryStatusHistory(DeliveryStatusHistoryId id, DeliveryId deliveryId, DeliveryStatus? previousStatus, DeliveryStatus newStatus, DeliveryChangeSource source, DateTime changedAtUtc, string? reasonCode, string? reasonText) : base(id)
    {
        DeliveryId = deliveryId; PreviousStatus = previousStatus; NewStatus = newStatus; Source = source; ChangedAtUtc = changedAtUtc; ReasonCode = reasonCode; ReasonText = reasonText;
    }

    public DeliveryId DeliveryId { get; private set; }
    public DeliveryStatus? PreviousStatus { get; private set; }
    public DeliveryStatus NewStatus { get; private set; }
    public DeliveryChangeSource Source { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? ReasonText { get; private set; }
    internal static DeliveryStatusHistory Create(DeliveryStatusHistoryId id, DeliveryId deliveryId, DeliveryStatus? previousStatus, DeliveryStatus newStatus, DeliveryChangeSource source, DateTime changedAtUtc, string? reasonCode, string? reasonText) => new(id, deliveryId, previousStatus, newStatus, source, changedAtUtc, reasonCode, reasonText);
}

public sealed class DeliveryProof : Entity<DeliveryProofId>
{
    private DeliveryProof() : base(default) { }
    private DeliveryProof(DeliveryProofId id, DeliveryId deliveryId, Guid driverId, DeliveryProofType type, Guid? mediaAssetId, string? recipientName, DateTime submittedAtUtc) : base(id)
    {
        DeliveryId = deliveryId; DriverId = driverId; Type = type; MediaAssetId = mediaAssetId; RecipientName = recipientName; SubmittedAtUtc = submittedAtUtc;
    }

    public DeliveryId DeliveryId { get; private set; }
    public Guid DriverId { get; private set; }
    public DeliveryProofType Type { get; private set; }
    public Guid? MediaAssetId { get; private set; }
    public string? RecipientName { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    internal static DeliveryProof Pin(DeliveryProofId id, DeliveryId deliveryId, Guid driverId, DateTime atUtc) => new(id, deliveryId, driverId, DeliveryProofType.Pin, null, null, atUtc);
    internal static DeliveryProof Media(DeliveryProofId id, DeliveryId deliveryId, Guid driverId, DeliveryProofType type, Guid mediaAssetId, DateTime atUtc) => mediaAssetId == Guid.Empty ? throw new DomainException("Media asset identifier is required.") : new(id, deliveryId, driverId, type, mediaAssetId, null, atUtc);
    internal static DeliveryProof Recipient(DeliveryProofId id, DeliveryId deliveryId, Guid driverId, string name, DateTime atUtc) => new(id, deliveryId, driverId, DeliveryProofType.RecipientName, null, name, atUtc);
}
