using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Orders.Domain;

public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusHistory> _statusHistory = [];
    private Order(OrderId id) : base(id) { }

    private Order(OrderId id, string orderNumber, Guid customerId, Guid merchantId, Guid? branchId, Guid sourceCartId, OrderType type, OrderPricingInput pricing, CustomerSnapshot customer, DeliveryAddressSnapshot address, MerchantSnapshot merchant, DateTime? scheduledForUtc, string? customerNotes, DateTime now) : base(id)
    {
        RequireGuid(customerId, "Customer"); RequireGuid(merchantId, "Merchant"); RequireGuid(sourceCartId, "Source cart"); RequireUtc(now);
        if (!Enum.IsDefined(type) || type == 0) throw new DomainException("Order type is invalid.");
        if (string.IsNullOrWhiteSpace(orderNumber) || orderNumber.Length > OrderRules.OrderNumberMaximumLength) throw new DomainException("Order number is invalid.");
        ValidatePricing(pricing);
        if (customer.CustomerId != customerId || merchant.MerchantId != merchantId || merchant.BranchId != branchId) throw new DomainException("Order snapshots do not match order references.");
        if (scheduledForUtc.HasValue) { RequireUtc(scheduledForUtc.Value); if (scheduledForUtc <= now) throw new DomainException("Scheduled order must be in the future."); }
        OrderNumber = orderNumber; CustomerId = customerId; MerchantId = merchantId; MerchantBranchId = branchId; SourceCartId = sourceCartId; Type = type;
        Status = OrderStatus.PendingPayment; Currency = pricing.Currency.Trim().ToUpperInvariant(); SubtotalMinor = pricing.SubtotalMinor; OptionsTotalMinor = pricing.OptionsTotalMinor;
        ProductDiscountMinor = pricing.ProductDiscountMinor; CouponDiscountMinor = pricing.CouponDiscountMinor; DeliveryDiscountMinor = pricing.DeliveryDiscountMinor;
        DeliveryFeeMinor = pricing.DeliveryFeeMinor; ServiceFeeMinor = pricing.ServiceFeeMinor; PlatformFeeMinor = pricing.PlatformFeeMinor; SmallOrderFeeMinor = pricing.SmallOrderFeeMinor; TaxMinor = pricing.TaxMinor; TotalMinor = pricing.TotalMinor;
        PricingReference = N(pricing.PricingReference); PricingCalculatedAtUtc = pricing.CalculatedAtUtc; Customer = customer; DeliveryAddress = address; Merchant = merchant;
        ScheduledForUtc = scheduledForUtc; CustomerNotes = Note(customerNotes, OrderRules.CustomerNotesMaximumLength); CreatedAtUtc = now; UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid();
        AddHistory(null, Status, now, null, OrderChangeSource.System, "order_created", null, null);
        RaiseDomainEvent(new OrderCreatedDomainEvent(id.Value, orderNumber, customerId, merchantId, sourceCartId, Status, TotalMinor, Currency, now));
    }

    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid? MerchantBranchId { get; private set; }
    public Guid SourceCartId { get; private set; }
    public OrderType Type { get; private set; }
    public OrderStatus Status { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public long SubtotalMinor { get; private set; }
    public long OptionsTotalMinor { get; private set; }
    public long ProductDiscountMinor { get; private set; }
    public long CouponDiscountMinor { get; private set; }
    public long DeliveryDiscountMinor { get; private set; }
    public long DeliveryFeeMinor { get; private set; }
    public long ServiceFeeMinor { get; private set; }
    public long PlatformFeeMinor { get; private set; }
    public long SmallOrderFeeMinor { get; private set; }
    public long TaxMinor { get; private set; }
    public long TotalMinor { get; private set; }
    public string? PricingReference { get; private set; }
    public DateTime PricingCalculatedAtUtc { get; private set; }
    public CustomerSnapshot Customer { get; private set; } = null!;
    public DeliveryAddressSnapshot DeliveryAddress { get; private set; } = null!;
    public MerchantSnapshot Merchant { get; private set; } = null!;
    public DateTime? ScheduledForUtc { get; private set; }
    public string? CustomerNotes { get; private set; }
    public string? MerchantNotes { get; private set; }
    public string? CancellationCode { get; private set; }
    public string? CancellationReason { get; private set; }
    public CancellationActor? CancelledBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public Guid? MerchantAcceptedByUserId { get; private set; }
    public int? EstimatedPreparationMinutes { get; private set; }
    public DateTime? EstimatedReadyAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public Guid? MerchantRejectedByUserId { get; private set; }
    public MerchantOrderRejectionReason? MerchantRejectionReason { get; private set; }
    public string? MerchantRejectionNote { get; private set; }
    public DateTime? PreparingAtUtc { get; private set; }
    public DateTime? ReadyForPickupAtUtc { get; private set; }
    public DateTime? DriverAssignedAtUtc { get; private set; }
    public DateTime? PickedUpAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public static Order Create(OrderId id, string orderNumber, Guid customerId, Guid merchantId, Guid? branchId, Guid sourceCartId, OrderType type, OrderPricingInput pricing, CustomerSnapshot customer, DeliveryAddressSnapshot address, MerchantSnapshot merchant, IEnumerable<OrderItemInput> items, DateTime? scheduledForUtc, string? customerNotes, DateTime now)
    {
        Order order = new(id, orderNumber, customerId, merchantId, branchId, sourceCartId, type, pricing, customer, address, merchant, scheduledForUtc, customerNotes, now);
        foreach (OrderItemInput input in items) order._items.Add(OrderItem.Create(OrderItemId.New(), id, input, order.Currency));
        if (order._items.Count == 0) throw new DomainException("Order must contain at least one item.");
        if (order._items.Sum(x => x.LineTotalMinor) != pricing.SubtotalMinor) throw new DomainException("Order item totals do not match pricing subtotal.");
        return order;
    }

    public void MarkPaymentAuthorized(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.PaymentAuthorized, atUtc, actor, OrderChangeSource.Payment, null, null, correlationId);
    public void Submit(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.Submitted, atUtc, actor, OrderChangeSource.System, null, null, correlationId);
    public void AcceptByMerchant(int preparationMinutes, DateTime atUtc, Guid actor, string? correlationId = null)
    {
        RequireActor(actor); ValidatePreparationMinutes(preparationMinutes);
        Transition(OrderStatus.AcceptedByMerchant, atUtc, actor, OrderChangeSource.Merchant, null, null, correlationId);
        MerchantAcceptedByUserId = actor; EstimatedPreparationMinutes = preparationMinutes; EstimatedReadyAtUtc = atUtc.AddMinutes(preparationMinutes);
        RaiseDomainEvent(new OrderAcceptedByMerchantDomainEvent(Id.Value, MerchantId, MerchantBranchId, actor, preparationMinutes, EstimatedReadyAtUtc.Value, atUtc));
    }
    public void AcceptByMerchant(DateTime atUtc, Guid actor, string? correlationId = null) => AcceptByMerchant(30, atUtc, actor, correlationId);
    public void RejectByMerchant(MerchantOrderRejectionReason reason, string? note, DateTime atUtc, Guid actor, string? correlationId = null)
    {
        RequireActor(actor); if (!Enum.IsDefined(reason) || reason == 0) throw new DomainException("Merchant rejection reason is invalid.");
        string? normalizedNote = Note(note, OrderRules.ReasonTextMaximumLength);
        if (reason == MerchantOrderRejectionReason.Other && normalizedNote is null) throw new DomainException("A rejection note is required for Other.");
        Transition(OrderStatus.RejectedByMerchant, atUtc, actor, OrderChangeSource.Merchant, reason.ToString(), normalizedNote, correlationId);
        MerchantRejectedByUserId = actor; MerchantRejectionReason = reason; MerchantRejectionNote = normalizedNote;
        RaiseDomainEvent(new OrderRejectedByMerchantDomainEvent(Id.Value, MerchantId, MerchantBranchId, actor, reason, atUtc));
    }
    public void RejectByMerchant(string reasonCode, string? reason, DateTime atUtc, Guid actor, string? correlationId = null)
    {
        if (!Enum.TryParse(reasonCode, true, out MerchantOrderRejectionReason parsed)) throw new DomainException("Merchant rejection reason is invalid.");
        RejectByMerchant(parsed, reason, atUtc, actor, correlationId);
    }
    public void UpdatePreparationTime(int preparationMinutes, DateTime atUtc, Guid actor, string? correlationId = null)
    {
        RequireUtc(atUtc); RequireActor(actor); ValidatePreparationMinutes(preparationMinutes);
        if (Status is not (OrderStatus.AcceptedByMerchant or OrderStatus.Preparing) || AcceptedAtUtc is null) throw new DomainException("Preparation time cannot be changed in the current state.");
        EstimatedPreparationMinutes = preparationMinutes; EstimatedReadyAtUtc = AcceptedAtUtc.Value.AddMinutes(preparationMinutes); Touch(atUtc);
        RaiseDomainEvent(new OrderPreparationTimeUpdatedDomainEvent(Id.Value, MerchantId, MerchantBranchId, actor, preparationMinutes, EstimatedReadyAtUtc.Value, atUtc));
    }
    public void StartPreparing(DateTime atUtc, Guid? actor = null, string? correlationId = null)
    {
        Guid actorId = actor ?? throw new DomainException("Merchant actor is required.");
        Transition(OrderStatus.Preparing, atUtc, actorId, OrderChangeSource.Merchant, null, null, correlationId);
        RaiseDomainEvent(new OrderPreparationStartedDomainEvent(Id.Value, MerchantId, MerchantBranchId, actorId, atUtc));
    }
    public void MarkReadyForPickup(DateTime atUtc, Guid? actor = null, string? correlationId = null)
    {
        Guid actorId = actor ?? throw new DomainException("Merchant actor is required.");
        Transition(OrderStatus.ReadyForPickup, atUtc, actorId, OrderChangeSource.Merchant, null, null, correlationId);
        RaiseDomainEvent(new OrderReadyForPickupDomainEvent(Id.Value, MerchantId, MerchantBranchId, actorId, atUtc));
    }
    public void StartDriverSearch(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.SearchingForDriver, atUtc, actor, OrderChangeSource.Delivery, null, null, correlationId);
    public void AssignDriver(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.DriverAssigned, atUtc, actor, OrderChangeSource.Delivery, null, null, correlationId);
    public void MarkDriverArrivingToPickup(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.DriverArrivingToPickup, atUtc, actor, OrderChangeSource.Delivery, null, null, correlationId);
    public void ConfirmPickup(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.PickedUp, atUtc, actor, OrderChangeSource.Delivery, null, null, correlationId);
    public void StartDelivery(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.OnTheWay, atUtc, actor, OrderChangeSource.Delivery, null, null, correlationId);
    public void MarkArrived(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.Arrived, atUtc, actor, OrderChangeSource.Delivery, null, null, correlationId);
    public void Deliver(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.Delivered, atUtc, actor, OrderChangeSource.Delivery, null, null, correlationId);
    public void MarkRefundPending(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.RefundPending, atUtc, actor, OrderChangeSource.Payment, null, null, correlationId);
    public void MarkRefunded(DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.Refunded, atUtc, actor, OrderChangeSource.Payment, null, null, correlationId);
    public void Fail(string reasonCode, string? reason, DateTime atUtc, Guid? actor = null, string? correlationId = null) => Transition(OrderStatus.Failed, atUtc, actor, OrderChangeSource.System, reasonCode, reason, correlationId);

    public void Cancel(CancellationActor actor, string reasonCode, string? reason, DateTime atUtc, Guid? actorUserId, string? correlationId)
    {
        RequireUtc(atUtc); if (!Enum.IsDefined(actor) || actor == 0) throw new DomainException("Cancellation actor is invalid.");
        if (Status is OrderStatus.Delivered or OrderStatus.RefundPending or OrderStatus.Refunded or OrderStatus.Cancelled or OrderStatus.PickedUp or OrderStatus.OnTheWay or OrderStatus.Arrived) throw new DomainException("Order cannot be cancelled in its current state.");
        string code = Required(reasonCode, OrderRules.ReasonCodeMaximumLength, "Cancellation reason code");
        CancellationCode = code; CancellationReason = Note(reason, OrderRules.ReasonTextMaximumLength); CancelledBy = actor; CancelledAtUtc = atUtc;
        Transition(OrderStatus.Cancelled, atUtc, actorUserId, (OrderChangeSource)(short)actor, code, CancellationReason, correlationId);
        RaiseDomainEvent(new OrderCancelledDomainEvent(Id.Value, Status, actor, code, atUtc));
    }

    private void Transition(OrderStatus next, DateTime atUtc, Guid? actor, OrderChangeSource source, string? reasonCode, string? reason, string? correlationId)
    {
        RequireUtc(atUtc); if (!Allowed(Status, next)) throw new DomainException($"Transition from {Status} to {next} is not allowed.");
        OrderStatus previous = Status; Status = next; UpdatedAtUtc = atUtc; ConcurrencyStamp = Guid.NewGuid();
        SubmittedAtUtc = next == OrderStatus.Submitted ? atUtc : SubmittedAtUtc; AcceptedAtUtc = next == OrderStatus.AcceptedByMerchant ? atUtc : AcceptedAtUtc;
        RejectedAtUtc = next == OrderStatus.RejectedByMerchant ? atUtc : RejectedAtUtc; PreparingAtUtc = next == OrderStatus.Preparing ? atUtc : PreparingAtUtc;
        ReadyForPickupAtUtc = next == OrderStatus.ReadyForPickup ? atUtc : ReadyForPickupAtUtc; DriverAssignedAtUtc = next == OrderStatus.DriverAssigned ? atUtc : DriverAssignedAtUtc;
        PickedUpAtUtc = next == OrderStatus.PickedUp ? atUtc : PickedUpAtUtc; DeliveredAtUtc = next == OrderStatus.Delivered ? atUtc : DeliveredAtUtc; FailedAtUtc = next == OrderStatus.Failed ? atUtc : FailedAtUtc;
        AddHistory(previous, next, atUtc, actor, source, reasonCode, reason, correlationId); RaiseDomainEvent(new OrderStatusChangedDomainEvent(Id.Value, previous, next, atUtc));
    }

    private void AddHistory(OrderStatus? previous, OrderStatus next, DateTime atUtc, Guid? actor, OrderChangeSource source, string? reasonCode, string? reason, string? correlationId) =>
        _statusHistory.Add(OrderStatusHistory.Create(OrderStatusHistoryId.New(), Id, previous, next, atUtc, actor, source, N(reasonCode), Note(reason, OrderRules.ReasonTextMaximumLength), N(correlationId)));

    private static bool Allowed(OrderStatus current, OrderStatus next) => current switch
    {
        OrderStatus.Draft => next is OrderStatus.PendingPayment or OrderStatus.Submitted,
        OrderStatus.PendingPayment => next is OrderStatus.PaymentAuthorized or OrderStatus.Cancelled or OrderStatus.Failed,
        OrderStatus.PaymentAuthorized => next is OrderStatus.Submitted or OrderStatus.Cancelled or OrderStatus.Failed,
        OrderStatus.Submitted => next is OrderStatus.AcceptedByMerchant or OrderStatus.RejectedByMerchant or OrderStatus.Cancelled,
        OrderStatus.AcceptedByMerchant => next is OrderStatus.Preparing or OrderStatus.Cancelled,
        OrderStatus.Preparing => next is OrderStatus.ReadyForPickup or OrderStatus.Cancelled,
        OrderStatus.ReadyForPickup => next is OrderStatus.SearchingForDriver or OrderStatus.DriverAssigned or OrderStatus.Cancelled,
        OrderStatus.SearchingForDriver => next is OrderStatus.DriverAssigned or OrderStatus.Cancelled or OrderStatus.Failed,
        OrderStatus.DriverAssigned => next is OrderStatus.DriverArrivingToPickup or OrderStatus.SearchingForDriver or OrderStatus.Cancelled,
        OrderStatus.DriverArrivingToPickup => next is OrderStatus.PickedUp or OrderStatus.SearchingForDriver or OrderStatus.Cancelled,
        OrderStatus.PickedUp => next == OrderStatus.OnTheWay,
        OrderStatus.OnTheWay => next == OrderStatus.Arrived,
        OrderStatus.Arrived => next is OrderStatus.Delivered or OrderStatus.Failed,
        OrderStatus.Delivered => next == OrderStatus.RefundPending,
        OrderStatus.RefundPending => next is OrderStatus.Refunded or OrderStatus.Failed,
        _ => false,
    };

    private static void ValidatePricing(OrderPricingInput p)
    {
        RequireUtc(p.CalculatedAtUtc); if (p.Currency.Trim().Length != OrderRules.CurrencyLength) throw new DomainException("Currency must be an ISO code.");
        long[] values = [p.SubtotalMinor, p.OptionsTotalMinor, p.ProductDiscountMinor, p.CouponDiscountMinor, p.DeliveryDiscountMinor, p.DeliveryFeeMinor, p.ServiceFeeMinor, p.PlatformFeeMinor, p.SmallOrderFeeMinor, p.TaxMinor, p.TotalMinor];
        if (values.Any(x => x < 0)) throw new DomainException("Money values cannot be negative.");
        long expected = checked(p.SubtotalMinor + p.DeliveryFeeMinor + p.ServiceFeeMinor + p.PlatformFeeMinor + p.SmallOrderFeeMinor + p.TaxMinor - p.ProductDiscountMinor - p.CouponDiscountMinor - p.DeliveryDiscountMinor);
        if (expected < 0 || expected != p.TotalMinor) throw new DomainException("Pricing total is inconsistent.");
    }
    private static void RequireUtc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new DomainException("Timestamp must be UTC."); }
    private static void RequireGuid(Guid value, string name) { if (value == Guid.Empty) throw new DomainException($"{name} identifier is required."); }
    private static void RequireActor(Guid actor) => RequireGuid(actor, "Actor");
    private static void ValidatePreparationMinutes(int value) { if (value is < OrderRules.PreparationMinutesMinimum or > OrderRules.PreparationMinutesMaximum) throw new DomainException("Preparation time is invalid."); }
    private void Touch(DateTime atUtc) { UpdatedAtUtc = atUtc; ConcurrencyStamp = Guid.NewGuid(); }
    private static string Required(string value, int max, string name) { string result = value?.Trim() ?? string.Empty; if (result.Length is 0 || result.Length > max) throw new DomainException($"{name} is invalid."); return result; }
    private static string? Note(string? value, int max) { string? result = N(value); if (result?.Length > max) throw new DomainException("Text is too long."); return result; }
    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class OrderItem : Entity<OrderItemId>
{
    private readonly List<OrderItemOption> _options = [];
    private OrderItem(OrderItemId id) : base(id) { }
    private OrderItem(OrderItemId id, OrderId orderId, OrderItemInput x) : base(id)
    {
        if (x.ProductId == Guid.Empty || x.ProductVersion < 1 || x.Quantity < 1 || string.IsNullOrWhiteSpace(x.ProductName)) throw new DomainException("Order item snapshot is invalid.");
        long[] money = [x.UnitBasePriceMinor, x.UnitOptionsPriceMinor, x.UnitDiscountMinor, x.UnitFinalPriceMinor, x.LineSubtotalMinor, x.LineDiscountMinor, x.LineTotalMinor];
        if (money.Any(v => v < 0) || x.LineTotalMinor != checked(x.UnitFinalPriceMinor * x.Quantity) || x.LineSubtotalMinor - x.LineDiscountMinor != x.LineTotalMinor) throw new DomainException("Order item pricing is invalid.");
        OrderId = orderId; ProductId = x.ProductId; ProductVersion = x.ProductVersion; VariantId = x.VariantId; ProductName = x.ProductName.Trim(); VariantName = N(x.VariantName); Sku = N(x.Sku); Quantity = x.Quantity;
        UnitBasePriceMinor = x.UnitBasePriceMinor; UnitOptionsPriceMinor = x.UnitOptionsPriceMinor; UnitDiscountMinor = x.UnitDiscountMinor; UnitFinalPriceMinor = x.UnitFinalPriceMinor; LineSubtotalMinor = x.LineSubtotalMinor; LineDiscountMinor = x.LineDiscountMinor; LineTotalMinor = x.LineTotalMinor; CustomerNote = N(x.CustomerNote);
    }
    public OrderId OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int ProductVersion { get; private set; }
    public Guid? VariantId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string? VariantName { get; private set; }
    public string? Sku { get; private set; }
    public int Quantity { get; private set; }
    public long UnitBasePriceMinor { get; private set; }
    public long UnitOptionsPriceMinor { get; private set; }
    public long UnitDiscountMinor { get; private set; }
    public long UnitFinalPriceMinor { get; private set; }
    public long LineSubtotalMinor { get; private set; }
    public long LineDiscountMinor { get; private set; }
    public long LineTotalMinor { get; private set; }
    public string? CustomerNote { get; private set; }
    public IReadOnlyCollection<OrderItemOption> Options => _options.AsReadOnly();
    internal static OrderItem Create(OrderItemId id, OrderId orderId, OrderItemInput input, string currency)
    {
        OrderItem item = new(id, orderId, input); foreach (OrderItemOptionInput option in input.Options) item._options.Add(OrderItemOption.Create(OrderItemOptionId.New(), id, option)); return item;
    }
    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class OrderItemOption : Entity<OrderItemOptionId>
{
    private OrderItemOption(OrderItemOptionId id) : base(id) { }
    private OrderItemOption(OrderItemOptionId id, OrderItemId itemId, OrderItemOptionInput x) : base(id)
    {
        if (x.OptionGroupId == Guid.Empty || x.OptionId == Guid.Empty || x.Quantity < 1 || string.IsNullOrWhiteSpace(x.OptionGroupName) || string.IsNullOrWhiteSpace(x.OptionName)) throw new DomainException("Order option snapshot is invalid.");
        if (x.TotalPriceAdjustmentMinor != checked(x.UnitPriceAdjustmentMinor * x.Quantity)) throw new DomainException("Order option pricing is invalid.");
        OrderItemId = itemId; OptionGroupId = x.OptionGroupId; OptionId = x.OptionId; OptionGroupName = x.OptionGroupName.Trim(); OptionName = x.OptionName.Trim(); Quantity = x.Quantity; UnitPriceAdjustmentMinor = x.UnitPriceAdjustmentMinor; TotalPriceAdjustmentMinor = x.TotalPriceAdjustmentMinor;
    }
    public OrderItemId OrderItemId { get; private set; }
    public Guid OptionGroupId { get; private set; }
    public Guid OptionId { get; private set; }
    public string OptionGroupName { get; private set; } = string.Empty;
    public string OptionName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public long UnitPriceAdjustmentMinor { get; private set; }
    public long TotalPriceAdjustmentMinor { get; private set; }
    internal static OrderItemOption Create(OrderItemOptionId id, OrderItemId itemId, OrderItemOptionInput input) => new(id, itemId, input);
}

public sealed class OrderStatusHistory : Entity<OrderStatusHistoryId>
{
    private OrderStatusHistory(OrderStatusHistoryId id) : base(id) { }
    private OrderStatusHistory(OrderStatusHistoryId id, OrderId orderId, OrderStatus? previous, OrderStatus next, DateTime changedAtUtc, Guid? changedBy, OrderChangeSource source, string? reasonCode, string? reasonText, string? correlationId) : base(id)
    { OrderId = orderId; PreviousStatus = previous; NewStatus = next; ChangedAtUtc = changedAtUtc; ChangedByUserId = changedBy; ChangeSource = source; ReasonCode = reasonCode; ReasonText = reasonText; CorrelationId = correlationId; }
    public OrderId OrderId { get; private set; }
    public OrderStatus? PreviousStatus { get; private set; }
    public OrderStatus NewStatus { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public OrderChangeSource ChangeSource { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? ReasonText { get; private set; }
    public string? CorrelationId { get; private set; }
    internal static OrderStatusHistory Create(OrderStatusHistoryId id, OrderId orderId, OrderStatus? previous, OrderStatus next, DateTime at, Guid? by, OrderChangeSource source, string? code, string? text, string? correlation) => new(id, orderId, previous, next, at, by, source, code, text, correlation);
}
