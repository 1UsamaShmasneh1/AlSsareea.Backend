using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Orders.Domain;

namespace AlSsareea.UnitTests.Orders;

public sealed class OrderDomainTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact] public void StrongIdsRejectEmpty() { Assert.Throws<DomainException>(() => new OrderId(Guid.Empty)); Assert.Throws<DomainException>(() => new OrderItemId(Guid.Empty)); Assert.Throws<DomainException>(() => new OrderStatusHistoryId(Guid.Empty)); Assert.Throws<DomainException>(() => new OrderOutboxMessageId(Guid.Empty)); }
    [Fact] public void StrongIdsHaveValueEquality() { Guid value = Guid.NewGuid(); Assert.Equal(new OrderId(value), new OrderId(value)); Assert.NotEqual(new OrderId(value), OrderId.New()); }
    [Fact]
    public void CreationBuildsPendingPaymentSnapshotHistoryAndEvent()
    {
        Order order = Create(); Assert.Equal(OrderStatus.PendingPayment, order.Status); Assert.Single(order.Items); Assert.Single(order.Items.Single().Options); Assert.Single(order.StatusHistory); Assert.IsType<OrderCreatedDomainEvent>(order.DomainEvents.Single()); Assert.Equal("Falafel", order.Items.Single().ProductName); Assert.Equal("Main Street", order.DeliveryAddress.Street);
    }
    [Fact] public void CreationRejectsEmptyItems() => Assert.Throws<DomainException>(() => Create(items: []));
    [Fact] public void CreationRejectsInconsistentTotal() => Assert.Throws<DomainException>(() => Create(pricing: Pricing(total: 999)));
    [Fact] public void CreationRejectsPastSchedule() => Assert.Throws<DomainException>(() => Create(scheduled: Now.AddMinutes(-1)));
    [Fact] public void CreationRequiresUtcSchedule() => Assert.Throws<DomainException>(() => Create(scheduled: DateTime.SpecifyKind(Now.AddHours(1), DateTimeKind.Local)));
    [Fact]
    public void FullLifecycleUsesExplicitTransitionsAndHistory()
    {
        Order x = Create(); Guid actor = Guid.NewGuid(); x.MarkPaymentAuthorized(Now.AddMinutes(1)); x.Submit(Now.AddMinutes(2)); x.AcceptByMerchant(Now.AddMinutes(3), actor); x.StartPreparing(Now.AddMinutes(4), actor); x.MarkReadyForPickup(Now.AddMinutes(5), actor); x.StartDriverSearch(Now.AddMinutes(6)); x.AssignDriver(Now.AddMinutes(7)); x.MarkDriverArrivingToPickup(Now.AddMinutes(8)); x.ConfirmPickup(Now.AddMinutes(9)); x.StartDelivery(Now.AddMinutes(10)); x.MarkArrived(Now.AddMinutes(11)); x.Deliver(Now.AddMinutes(12)); x.MarkRefundPending(Now.AddMinutes(13)); x.MarkRefunded(Now.AddMinutes(14));
        Assert.Equal(OrderStatus.Refunded, x.Status); Assert.Equal(15, x.StatusHistory.Count); Assert.Equal(Now.AddMinutes(12), x.DeliveredAtUtc);
    }
    [Fact] public void InvalidTransitionIsRejected() { Order x = Create(); Assert.Throws<DomainException>(() => x.Deliver(Now.AddMinutes(1))); Assert.Equal(OrderStatus.PendingPayment, x.Status); }
    [Fact] public void RepeatingTransitionIsRejected() { Order x = Create(); x.MarkPaymentAuthorized(Now.AddMinutes(1)); Assert.Throws<DomainException>(() => x.MarkPaymentAuthorized(Now.AddMinutes(2))); }
    [Fact]
    public void CustomerCanCancelBeforeAcceptance()
    {
        Order x = Create(); Guid stamp = x.ConcurrencyStamp; x.Cancel(CancellationActor.Customer, "changed_mind", "No longer needed", Now.AddMinutes(1), Guid.NewGuid(), "corr-1"); Assert.Equal(OrderStatus.Cancelled, x.Status); Assert.NotEqual(stamp, x.ConcurrencyStamp); Assert.Equal("changed_mind", x.CancellationCode); Assert.Equal(2, x.StatusHistory.Count); Assert.Contains(x.DomainEvents, e => e is OrderCancelledDomainEvent);
    }
    [Fact]
    public void DeliveredOrderCannotBeCancelled()
    {
        Order x = Create(); AdvanceToDelivered(x); Assert.Throws<DomainException>(() => x.Cancel(CancellationActor.Customer, "late", null, Now.AddHours(1), Guid.NewGuid(), null));
    }
    [Fact]
    public void InvalidCancellationTimestampDoesNotMutateOrder()
    {
        Order x = Create(); Assert.Throws<DomainException>(() => x.Cancel(CancellationActor.Customer, "changed", null, DateTime.SpecifyKind(Now.AddMinutes(1), DateTimeKind.Local), Guid.NewGuid(), null)); Assert.Equal(OrderStatus.PendingPayment, x.Status); Assert.Null(x.CancellationCode);
    }
    [Fact] public void SnapshotCollectionsAreReadOnlyViews() { Order x = Create(); Assert.IsAssignableFrom<IReadOnlyCollection<OrderItem>>(x.Items); Assert.IsAssignableFrom<IReadOnlyCollection<OrderItemOption>>(x.Items.Single().Options); }
    [Fact] public void NegativeMoneyIsRejected() => Assert.Throws<DomainException>(() => Create(pricing: Pricing(subtotal: -1, total: -1)));
    [Fact] public void InvalidCurrencyIsRejected() => Assert.Throws<DomainException>(() => Create(pricing: Pricing(currency: "US")));
    [Fact] public void ZeroQuantityIsRejected() { OrderItemInput bad = Item() with { Quantity = 0, LineTotalMinor = 0 }; Assert.Throws<DomainException>(() => Create(items: [bad], pricing: Pricing(subtotal: 0, total: 200))); }

    private static Order Create(IReadOnlyList<OrderItemInput>? items = null, OrderPricingInput? pricing = null, DateTime? scheduled = null)
    {
        Guid customer = Guid.NewGuid(); Guid merchant = Guid.NewGuid(); Guid branch = Guid.NewGuid();
        return Order.Create(OrderId.New(), Guid.NewGuid().ToString("N").ToUpperInvariant(), customer, merchant, branch, Guid.NewGuid(), OrderType.Restaurant, pricing ?? Pricing(), new CustomerSnapshot(customer, "Customer", null, "ar"), new DeliveryAddressSnapshot(Guid.NewGuid(), "Home", "City", "Area", "Main Street", "1", null, null, null, 31.5, 35.0, null, "Main Street, City"), new MerchantSnapshot(merchant, branch, "Merchant", "Branch", "Branch street", null), items ?? [Item()], scheduled, "note", Now);
    }
    private static OrderItemInput Item() => new(Guid.NewGuid(), 3, null, "Falafel", null, "SKU-1", 2, 450, 50, 0, 500, 1000, 0, 1000, null, [new(Guid.NewGuid(), Guid.NewGuid(), "Extras", "Tahini", 1, 50, 50)]);
    private static OrderPricingInput Pricing(long subtotal = 1000, long total = 1200, string currency = "ILS") => new(subtotal, 100, 0, 0, 0, 100, 50, 25, 0, 25, total, currency, "price:1", Now);
    private static void AdvanceToDelivered(Order x) { Guid actor = Guid.NewGuid(); x.MarkPaymentAuthorized(Now.AddMinutes(1)); x.Submit(Now.AddMinutes(2)); x.AcceptByMerchant(Now.AddMinutes(3), actor); x.StartPreparing(Now.AddMinutes(4), actor); x.MarkReadyForPickup(Now.AddMinutes(5), actor); x.AssignDriver(Now.AddMinutes(6)); x.MarkDriverArrivingToPickup(Now.AddMinutes(7)); x.ConfirmPickup(Now.AddMinutes(8)); x.StartDelivery(Now.AddMinutes(9)); x.MarkArrived(Now.AddMinutes(10)); x.Deliver(Now.AddMinutes(11)); }
}
