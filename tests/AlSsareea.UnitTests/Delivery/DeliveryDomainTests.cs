using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Delivery.Domain;
using DeliveryAggregate = AlSsareea.Modules.Delivery.Domain.Delivery;

namespace AlSsareea.UnitTests.Delivery;

public sealed class DeliveryDomainTests
{
    [Fact]
    public void CreateCapturesSnapshotsTimelineConcurrencyAndEvent()
    {
        DateTime now = DateTime.UtcNow;
        DeliveryAggregate delivery = Create(ProofRequirement.Pin | ProofRequirement.Photo, now);
        Assert.Equal(DeliveryStatus.Created, delivery.Status);
        Assert.Equal("Merchant address", delivery.Pickup.Address);
        Assert.Equal("Customer address", delivery.DropOff.Address);
        Assert.NotEqual(Guid.Empty, delivery.ConcurrencyStamp);
        Assert.IsType<DeliveryCreatedDomainEvent>(Assert.Single(delivery.DomainEvents));
        Assert.Equal(DeliveryStatus.Created, Assert.Single(delivery.StatusHistory).NewStatus);
    }

    [Fact]
    public void CreateRejectsMissingReferencesInvalidCoordinatesAndMissingPinConfiguration()
    {
        DateTime now = DateTime.UtcNow;
        Assert.Throws<DomainException>(() => DeliveryAggregate.Create(DeliveryId.New(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Pickup(), DropOff(), ProofRequirement.None, null, null, now));
        Assert.Throws<DomainException>(() => new PickupSnapshot(Guid.NewGuid(), null, "Address", null, null, null, 100, 35));
        Assert.Throws<DomainException>(() => DeliveryAggregate.Create(DeliveryId.New(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Pickup(), DropOff(), ProofRequirement.Pin, null, null, now));
    }

    [Fact]
    public void ValidJourneyRequiresEveryStateAndRecordsTimestamps()
    {
        DateTime now = DateTime.UtcNow; Guid driver = Guid.NewGuid(); DeliveryAggregate delivery = Create(ProofRequirement.None, now);
        delivery.Assign(driver, now.AddMinutes(1)); delivery.BeginHeadingToPickup(now.AddMinutes(2)); delivery.ArriveAtPickup(now.AddMinutes(3)); delivery.ConfirmPickup(now.AddMinutes(4)); delivery.Start(now.AddMinutes(5)); delivery.ArriveAtDropOff(now.AddMinutes(6)); delivery.Complete(now.AddMinutes(7));
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(now.AddMinutes(4), delivery.PickedUpAtUtc);
        Assert.Equal(now.AddMinutes(7), delivery.DeliveredAtUtc);
        Assert.Equal(8, delivery.StatusHistory.Count);
        Assert.Contains(delivery.DomainEvents, x => x is DeliveryCompletedDomainEvent);
    }

    [Fact]
    public void InvalidTransitionsAreRejected()
    {
        DeliveryAggregate delivery = Create(ProofRequirement.None, DateTime.UtcNow);
        Assert.Throws<DomainException>(() => delivery.ConfirmPickup(DateTime.UtcNow));
        delivery.Assign(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Throws<DomainException>(() => delivery.Start(DateTime.UtcNow));
        Assert.Throws<DomainException>(() => delivery.Complete(DateTime.UtcNow));
        Assert.Throws<DomainException>(() => delivery.Assign(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void CompletionRequiresAllConfiguredProofs()
    {
        DateTime now = DateTime.UtcNow; DeliveryAggregate delivery = AtDropOff(ProofRequirement.Pin | ProofRequirement.Photo | ProofRequirement.Signature | ProofRequirement.RecipientName, now);
        Assert.Throws<DomainException>(() => delivery.Complete(now.AddMinutes(7)));
        delivery.RecordPinAttempt(true, now.AddMinutes(7));
        delivery.AddMediaProof(DeliveryProofType.Photo, Guid.NewGuid(), now.AddMinutes(8));
        delivery.AddMediaProof(DeliveryProofType.Signature, Guid.NewGuid(), now.AddMinutes(9));
        delivery.AddRecipientName("  Recipient  ", now.AddMinutes(10));
        delivery.Complete(now.AddMinutes(11));
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(4, delivery.Proofs.Count);
        Assert.Equal("Recipient", delivery.Proofs.Single(x => x.Type == DeliveryProofType.RecipientName).RecipientName);
    }

    [Fact]
    public void PinAttemptsLockAndDoNotStoreCandidate()
    {
        DateTime now = DateTime.UtcNow; DeliveryAggregate delivery = AtDropOff(ProofRequirement.Pin, now);
        for (int i = 0; i < DeliveryRules.MaximumPinAttempts; i++) delivery.RecordPinAttempt(false, now.AddMinutes(7 + i));
        Assert.True(delivery.PinLocked);
        Assert.Empty(delivery.Proofs);
        Assert.Throws<DomainException>(() => delivery.RecordPinAttempt(true, now.AddMinutes(20)));
    }

    [Fact]
    public void FailureIsControlledTerminalAndRecordedInTimeline()
    {
        DateTime now = DateTime.UtcNow; DeliveryAggregate delivery = Create(ProofRequirement.None, now); delivery.Assign(Guid.NewGuid(), now.AddMinutes(1));
        delivery.Fail(DeliveryFailureReason.SafetyIssue, "Unsafe entrance", now.AddMinutes(2));
        Assert.Equal(DeliveryStatus.Failed, delivery.Status); Assert.Equal(DeliveryFailureReason.SafetyIssue, delivery.FailureReason); Assert.Equal("SafetyIssue", delivery.StatusHistory.Last().ReasonCode);
        Assert.Throws<DomainException>(() => delivery.Start(now.AddMinutes(3))); Assert.Throws<DomainException>(() => delivery.Fail(DeliveryFailureReason.Other, null, now.AddMinutes(3)));
    }

    [Theory]
    [InlineData(DeliveryStatus.Created, false)]
    [InlineData(DeliveryStatus.Assigned, false)]
    [InlineData(DeliveryStatus.PickedUp, true)]
    [InlineData(DeliveryStatus.InTransit, true)]
    [InlineData(DeliveryStatus.ArrivedAtDropOff, true)]
    [InlineData(DeliveryStatus.Delivered, false)]
    public void TrackingVisibilityIsExplicit(DeliveryStatus target, bool visible)
    {
        DateTime now = DateTime.UtcNow; DeliveryAggregate delivery = Create(ProofRequirement.None, now);
        if (target >= DeliveryStatus.Assigned) delivery.Assign(Guid.NewGuid(), now.AddMinutes(1));
        if (target >= DeliveryStatus.HeadingToPickup) delivery.BeginHeadingToPickup(now.AddMinutes(2));
        if (target >= DeliveryStatus.ArrivedAtPickup) delivery.ArriveAtPickup(now.AddMinutes(3));
        if (target >= DeliveryStatus.PickedUp) delivery.ConfirmPickup(now.AddMinutes(4));
        if (target >= DeliveryStatus.InTransit) delivery.Start(now.AddMinutes(5));
        if (target >= DeliveryStatus.ArrivedAtDropOff) delivery.ArriveAtDropOff(now.AddMinutes(6));
        if (target == DeliveryStatus.Delivered) delivery.Complete(now.AddMinutes(7));
        Assert.Equal(visible, delivery.IsCustomerTrackingVisible);
    }

    private static DeliveryAggregate AtDropOff(ProofRequirement requirements, DateTime now)
    {
        DeliveryAggregate delivery = Create(requirements, now); delivery.Assign(Guid.NewGuid(), now.AddMinutes(1)); delivery.BeginHeadingToPickup(now.AddMinutes(2)); delivery.ArriveAtPickup(now.AddMinutes(3)); delivery.ConfirmPickup(now.AddMinutes(4)); delivery.Start(now.AddMinutes(5)); delivery.ArriveAtDropOff(now.AddMinutes(6)); return delivery;
    }
    private static DeliveryAggregate Create(ProofRequirement requirements, DateTime now) => DeliveryAggregate.Create(DeliveryId.New(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Pickup(), DropOff(), requirements, (requirements & ProofRequirement.Pin) != 0 ? "hash" : null, (requirements & ProofRequirement.Pin) != 0 ? "salt" : null, now);
    private static PickupSnapshot Pickup() => new(Guid.NewGuid(), Guid.NewGuid(), "Merchant address", "Merchant", "+970000000", "Back entrance", 31.7, 35.2);
    private static DropOffSnapshot DropOff() => new(Guid.NewGuid(), "Customer address", "Customer", "+970000001", "2", "Call on arrival", 31.8, 35.3);
}
