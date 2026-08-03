using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Orders.Domain;

namespace AlSsareea.UnitTests.Orders;

public sealed class MerchantOrderOperationsDomainTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AcceptCapturesActorPreparationHistoryEventAndConcurrency()
    {
        Order order = Submitted(); Guid actor = Guid.NewGuid(); Guid stamp = order.ConcurrencyStamp;
        order.AcceptByMerchant(25, Now.AddMinutes(3), actor, "corr-1");
        Assert.Equal(OrderStatus.AcceptedByMerchant, order.Status); Assert.Equal(actor, order.MerchantAcceptedByUserId);
        Assert.Equal(25, order.EstimatedPreparationMinutes); Assert.Equal(Now.AddMinutes(28), order.EstimatedReadyAtUtc);
        Assert.Equal(4, order.StatusHistory.Count); Assert.NotEqual(stamp, order.ConcurrencyStamp);
        Assert.Contains(order.DomainEvents, x => x is OrderAcceptedByMerchantDomainEvent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(241)]
    public void AcceptRejectsInvalidPreparationMinutes(int minutes) => Assert.Throws<DomainException>(() => Submitted().AcceptByMerchant(minutes, Now.AddMinutes(3), Guid.NewGuid()));

    [Fact] public void AcceptRejectsInvalidState() { Order order = Submitted(); order.AcceptByMerchant(10, Now.AddMinutes(3), Guid.NewGuid()); Assert.Throws<DomainException>(() => order.AcceptByMerchant(10, Now.AddMinutes(4), Guid.NewGuid())); }

    [Fact]
    public void RejectCapturesStableReasonActorHistoryAndEvent()
    {
        Order order = Submitted(); Guid actor = Guid.NewGuid(); order.RejectByMerchant(MerchantOrderRejectionReason.ItemUnavailable, "Out of stock", Now.AddMinutes(3), actor);
        Assert.Equal(OrderStatus.RejectedByMerchant, order.Status); Assert.Equal(actor, order.MerchantRejectedByUserId);
        Assert.Equal(MerchantOrderRejectionReason.ItemUnavailable, order.MerchantRejectionReason); Assert.Equal("Out of stock", order.MerchantRejectionNote);
        Assert.Contains(order.DomainEvents, x => x is OrderRejectedByMerchantDomainEvent);
    }

    [Fact] public void RejectRequiresOtherNote() => Assert.Throws<DomainException>(() => Submitted().RejectByMerchant(MerchantOrderRejectionReason.Other, null, Now.AddMinutes(3), Guid.NewGuid()));
    [Fact] public void RejectAfterAcceptIsPrevented() { Order order = Submitted(); order.AcceptByMerchant(10, Now.AddMinutes(3), Guid.NewGuid()); Assert.Throws<DomainException>(() => order.RejectByMerchant(MerchantOrderRejectionReason.CannotFulfill, null, Now.AddMinutes(4), Guid.NewGuid())); }
    [Fact] public void PreparationCannotStartBeforeAccept() => Assert.Throws<DomainException>(() => Submitted().StartPreparing(Now.AddMinutes(3), Guid.NewGuid()));
    [Fact] public void ReadyCannotSkipPreparing() { Order order = Submitted(); order.AcceptByMerchant(10, Now.AddMinutes(3), Guid.NewGuid()); Assert.Throws<DomainException>(() => order.MarkReadyForPickup(Now.AddMinutes(4), Guid.NewGuid())); }

    [Fact]
    public void PreparationTimeUpdateUsesAcceptanceAsStableBase()
    {
        Order order = Submitted(); Guid actor = Guid.NewGuid(); order.AcceptByMerchant(20, Now.AddMinutes(3), actor);
        order.UpdatePreparationTime(45, Now.AddMinutes(8), actor);
        Assert.Equal(Now.AddMinutes(48), order.EstimatedReadyAtUtc);
        Assert.Contains(order.DomainEvents, x => x is OrderPreparationTimeUpdatedDomainEvent);
    }

    [Fact]
    public void PreparingThenReadyRequiresOrderedTransitions()
    {
        Order order = Submitted(); Guid actor = Guid.NewGuid(); order.AcceptByMerchant(20, Now.AddMinutes(3), actor);
        order.StartPreparing(Now.AddMinutes(4), actor); order.MarkReadyForPickup(Now.AddMinutes(5), actor);
        Assert.Equal(OrderStatus.ReadyForPickup, order.Status); Assert.Equal(Now.AddMinutes(5), order.ReadyForPickupAtUtc);
        Assert.Contains(order.DomainEvents, x => x is OrderPreparationStartedDomainEvent);
        Assert.Contains(order.DomainEvents, x => x is OrderReadyForPickupDomainEvent);
        Assert.Throws<DomainException>(() => order.MarkReadyForPickup(Now.AddMinutes(6), actor));
        Assert.Throws<DomainException>(() => order.UpdatePreparationTime(15, Now.AddMinutes(6), actor));
    }

    private static Order Submitted()
    {
        Guid customer = Guid.NewGuid(); Guid merchant = Guid.NewGuid(); Guid branch = Guid.NewGuid();
        OrderItemInput item = new(Guid.NewGuid(), 1, null, "Item", null, null, 1, 1000, 0, 0, 1000, 1000, 0, 1000, null, []);
        Order order = Order.Create(OrderId.New(), Guid.NewGuid().ToString("N").ToUpperInvariant(), customer, merchant, branch, Guid.NewGuid(), OrderType.Restaurant,
            new(1000, 0, 0, 0, 0, 100, 50, 25, 0, 25, 1200, "ILS", null, Now),
            new(customer, "Customer", null, "ar"), new(Guid.NewGuid(), "Home", "City", null, "Street", null, null, null, null, null, null, null, null),
            new(merchant, branch, "Merchant", "Branch", null, null), [item], null, null, Now);
        order.MarkPaymentAuthorized(Now.AddMinutes(1)); order.Submit(Now.AddMinutes(2)); return order;
    }
}
