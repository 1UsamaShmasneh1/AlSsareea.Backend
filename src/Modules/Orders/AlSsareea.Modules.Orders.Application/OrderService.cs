using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Carts.Contracts;
using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;

namespace AlSsareea.Modules.Orders.Application;

public sealed partial class OrderService(IOrderRepository repository, IOrderCheckoutProvider checkout, IOrderCustomerSnapshotProvider customers, IOrderMerchantSnapshotProvider merchants, IClock clock) : IOrderService
{
    private const string CreateOperation = "order.create";

    public async Task<OrderOperationResult<CreateOrderResponse>> CreateAsync(Guid userId, CreateOrderRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (userId == Guid.Empty || request.CartId == Guid.Empty || request.DeliveryAddressId == Guid.Empty || !Enum.IsDefined((OrderType)request.OrderType) || request.OrderType == 0 || !ValidKey(idempotencyKey)) return Fail<CreateOrderResponse>(OrderOperationStatus.Invalid, OrderErrorCodes.InvalidRequest);
        Guid? resolvedCustomerId = await customers.GetCustomerIdAsync(userId, ct);
        if (!resolvedCustomerId.HasValue) return Fail<CreateOrderResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.InvalidAddress);
        string keyHash = Hash(idempotencyKey); string requestHash = Hash(request);
        OrderIdempotencyResult? duplicate = await repository.FindIdempotencyAsync(resolvedCustomerId.Value, CreateOperation, keyHash, ct);
        if (duplicate is not null)
        {
            if (duplicate.RequestHash != requestHash) return Fail<CreateOrderResponse>(OrderOperationStatus.Conflict, OrderErrorCodes.IdempotencyConflict);
            _ = await checkout.MarkConvertedAsync(userId, request.CartId, duplicate.OrderId, ct);
            return await ExistingCreate(duplicate.OrderId, resolvedCustomerId.Value, ct);
        }
        OrderCustomerSnapshotContract? customer = await customers.GetAsync(userId, request.DeliveryAddressId, ct);
        if (customer is null || customer.CustomerId != resolvedCustomerId.Value) return Fail<CreateOrderResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.InvalidAddress);

        OrderCheckoutResult checkoutResult = await checkout.GetTrustedSummaryAsync(userId, request.CartId, request.ExpectedCartVersion, ct);
        if (checkoutResult.Status == OrderCheckoutStatus.Conflict) return Fail<CreateOrderResponse>(OrderOperationStatus.Conflict, OrderErrorCodes.ConcurrencyConflict);
        CartCheckoutSummaryResponse? summary = checkoutResult.Summary;
        if (summary is null || !summary.IsCheckoutReady || summary.CustomerId != customer.CustomerId || summary.CartId != request.CartId || summary.ExpiresAtUtc <= clock.UtcNow || string.IsNullOrWhiteSpace(summary.Currency) || summary.Items.Count == 0) return Fail<CreateOrderResponse>(OrderOperationStatus.Unprocessable, checkoutResult.ErrorCode ?? OrderErrorCodes.InvalidCart);
        OrderMerchantSnapshotContract? merchant = await merchants.GetAsync(summary.MerchantId, summary.BranchId, ct);
        if (merchant is null) return Fail<CreateOrderResponse>(OrderOperationStatus.Unprocessable, OrderErrorCodes.MerchantUnavailable);

        try
        {
            DateTime now = clock.UtcNow; OrderId id = OrderId.New(); string number = id.Value.ToString("N").ToUpperInvariant();
            long optionsTotal = summary.Items.Sum(x => checked(x.UnitOptionsPriceMinor * x.Quantity));
            OrderPricingInput pricing = new(summary.SubtotalMinor, optionsTotal, summary.PromotionDiscountMinor, 0, 0, summary.DeliveryFeeMinor, summary.ServiceFeeMinor, summary.OtherFeesMinor, 0, summary.TaxMinor, summary.GrandTotalMinor, summary.Currency, summary.PricingReference, summary.CalculatedAtUtc);
            CustomerSnapshot customerSnapshot = new(customer.CustomerId, customer.DisplayName, customer.PhoneNumber, customer.PreferredLanguage);
            OrderAddressSnapshotContract a = customer.Address; DeliveryAddressSnapshot address = new(a.AddressId, a.Label, a.City, a.Area, a.Street, a.BuildingNumber, a.Floor, a.Apartment, a.DeliveryInstructions, a.Latitude, a.Longitude, a.PlaceId, a.FormattedAddress);
            MerchantSnapshot merchantSnapshot = new(merchant.MerchantId, merchant.BranchId, merchant.MerchantDisplayName, merchant.BranchDisplayName, merchant.BranchAddress, merchant.BranchPhoneNumber);
            OrderItemInput[] items = summary.Items.Select(x => new OrderItemInput(x.ProductId, x.ProductVersion, x.VariantId, x.ProductName ?? "Product", x.VariantName, x.Sku, x.Quantity, x.UnitBasePriceMinor, x.UnitOptionsPriceMinor, 0, x.UnitPriceMinor, x.LineSubtotalMinor, x.LineDiscountMinor, x.LineTotalMinor, x.CustomerNote, x.Options.Select(o => new OrderItemOptionInput(o.OptionGroupId, o.OptionId, o.OptionGroupName, o.OptionName, o.Quantity, o.UnitPriceAdjustmentMinor, o.TotalPriceAdjustmentMinor)).ToArray())).ToArray();
            Order order = Order.Create(id, number, customer.CustomerId, summary.MerchantId, summary.BranchId, summary.CartId, (OrderType)request.OrderType, pricing, customerSnapshot, address, merchantSnapshot, items, request.ScheduledForUtc, request.CustomerNotes, now);
            OrderCreatedIntegrationEvent createdEvent = new(Guid.NewGuid(), 1, id.Value, number, customer.CustomerId, summary.MerchantId, summary.BranchId, summary.CartId, (short)order.Status, order.TotalMinor, order.Currency, now);
            OrderCreatePersistenceResult saved = await repository.CreateAsync(order, customer.CustomerId, CreateOperation, keyHash, requestHash, [createdEvent], ct);
            if (saved == OrderCreatePersistenceResult.DuplicateDifferentRequest) return Fail<CreateOrderResponse>(OrderOperationStatus.Conflict, OrderErrorCodes.IdempotencyConflict);
            if (saved != OrderCreatePersistenceResult.Created)
            {
                OrderIdempotencyResult? raced = await repository.FindIdempotencyAsync(customer.CustomerId, CreateOperation, keyHash, ct);
                if (raced?.RequestHash != requestHash) return Fail<CreateOrderResponse>(OrderOperationStatus.Conflict, OrderErrorCodes.IdempotencyConflict);
                _ = await checkout.MarkConvertedAsync(userId, request.CartId, raced.OrderId, ct);
                return await ExistingCreate(raced.OrderId, customer.CustomerId, ct);
            }
            _ = await checkout.MarkConvertedAsync(userId, summary.CartId, id.Value, ct);
            return new(OrderOperationStatus.Created, new(id.Value, number, (short)order.Status, order.Currency, order.TotalMinor, order.CreatedAtUtc));
        }
        catch (DomainException) { return Fail<CreateOrderResponse>(OrderOperationStatus.Invalid, OrderErrorCodes.InvalidRequest); }
        catch (OverflowException) { return Fail<CreateOrderResponse>(OrderOperationStatus.Invalid, OrderErrorCodes.InvalidRequest); }
    }

    public async Task<OrderOperationResult<OrderDetailsResponse>> GetAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        Guid? customerId = await CustomerId(userId, ct); if (!customerId.HasValue) return Fail<OrderDetailsResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        OrderDetailsResponse? value = await repository.GetDetailsAsync(orderId, customerId.Value, ct); return value is null ? Fail<OrderDetailsResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound) : Success(value);
    }
    public async Task<OrderOperationResult<OrderDetailsResponse>> GetByNumberAsync(Guid userId, string orderNumber, CancellationToken ct)
    {
        Guid? customerId = await CustomerId(userId, ct); if (!customerId.HasValue || string.IsNullOrWhiteSpace(orderNumber)) return Fail<OrderDetailsResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        OrderDetailsResponse? value = await repository.GetDetailsByNumberAsync(orderNumber.Trim().ToUpperInvariant(), customerId.Value, ct); return value is null ? Fail<OrderDetailsResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound) : Success(value);
    }
    public async Task<OrderOperationResult<OrderListResponse>> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        Guid? customerId = await CustomerId(userId, ct); if (!customerId.HasValue) return Fail<OrderListResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        if (page < 1 || pageSize is < 1 or > 100) return Fail<OrderListResponse>(OrderOperationStatus.Invalid, OrderErrorCodes.InvalidRequest);
        return Success(await repository.ListAsync(customerId.Value, page, pageSize, ct));
    }
    public async Task<OrderOperationResult<IReadOnlyList<OrderTimelineEntryResponse>>> TimelineAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        Guid? customerId = await CustomerId(userId, ct); if (!customerId.HasValue) return Fail<IReadOnlyList<OrderTimelineEntryResponse>>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        IReadOnlyList<OrderTimelineEntryResponse>? value = await repository.TimelineAsync(orderId, customerId.Value, ct); return value is null ? Fail<IReadOnlyList<OrderTimelineEntryResponse>>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound) : Success(value);
    }
    public async Task<OrderOperationResult<OrderDetailsResponse>> CancelAsync(Guid userId, Guid orderId, CancelOrderRequest request, string? correlationId, CancellationToken ct)
    {
        Guid? customerId = await CustomerId(userId, ct); if (!customerId.HasValue) return Fail<OrderDetailsResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        Order? order = await repository.GetForUpdateAsync(new OrderId(orderId), ct); if (order is null || order.CustomerId != customerId.Value) return Fail<OrderDetailsResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        if (order.ConcurrencyStamp != request.ConcurrencyStamp) return Fail<OrderDetailsResponse>(OrderOperationStatus.Conflict, OrderErrorCodes.ConcurrencyConflict);
        if ((CancellationActor)request.Actor != CancellationActor.Customer) return Fail<OrderDetailsResponse>(OrderOperationStatus.Forbidden, OrderErrorCodes.Forbidden);
        try
        {
            OrderStatus previous = order.Status; DateTime now = clock.UtcNow; order.Cancel(CancellationActor.Customer, request.ReasonCode, request.Reason, now, userId, correlationId);
            OrderCancelledIntegrationEvent cancelled = new(Guid.NewGuid(), 1, order.Id.Value, order.CustomerId, order.MerchantId, (short)previous, request.Actor, request.ReasonCode, now);
            if (!await repository.SaveAsync([cancelled], ct)) return Fail<OrderDetailsResponse>(OrderOperationStatus.Conflict, OrderErrorCodes.ConcurrencyConflict);
            OrderDetailsResponse? value = await repository.GetDetailsAsync(orderId, customerId.Value, ct); return value is null ? Fail<OrderDetailsResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound) : Success(value);
        }
        catch (DomainException) { return Fail<OrderDetailsResponse>(OrderOperationStatus.Conflict, OrderErrorCodes.InvalidTransition); }
    }

    private async Task<Guid?> CustomerId(Guid userId, CancellationToken ct) => await customers.GetCustomerIdAsync(userId, ct);
    private async Task<OrderOperationResult<CreateOrderResponse>> ExistingCreate(Guid orderId, Guid customerId, CancellationToken ct)
    {
        OrderDetailsResponse? x = await repository.GetDetailsAsync(orderId, customerId, ct); return x is null ? Fail<CreateOrderResponse>(OrderOperationStatus.Conflict, OrderErrorCodes.IdempotencyConflict) : Success(new CreateOrderResponse(x.Id, x.OrderNumber, x.Status, x.Currency, x.TotalMinor, x.CreatedAtUtc));
    }
    private static bool ValidKey(string key) => key.Length is >= 8 and <= OrderRules.IdempotencyKeyMaximumLength && IdempotencyKeyRegex().IsMatch(key);
    private static string Hash(object value) { string text = value is string s ? s : JsonSerializer.Serialize(value); return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))); }
    private static OrderOperationResult<T> Success<T>(T value) => new(OrderOperationStatus.Success, value);
    private static OrderOperationResult<T> Fail<T>(OrderOperationStatus status, string code) => new(status, default, code);
    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)] private static partial Regex IdempotencyKeyRegex();
}
