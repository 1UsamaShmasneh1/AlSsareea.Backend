using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Orders.Infrastructure.Persistence;

internal sealed class OrderRepository(OrdersDbContext db, IClock clock) : IOrderRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OrderIdempotencyResult?> FindIdempotencyAsync(Guid customerId, string operation, string keyHash, CancellationToken ct) =>
        await db.IdempotencyRecords.AsNoTracking().Where(x => x.CustomerId == customerId && x.Operation == operation && x.KeyHash == keyHash).Select(x => new OrderIdempotencyResult(x.OrderId.Value, x.RequestHash)).SingleOrDefaultAsync(ct);

    public async Task<OrderCreatePersistenceResult> CreateAsync(Order order, Guid customerId, string operation, string keyHash, string requestHash, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken ct)
    {
        db.Orders.Add(order); db.IdempotencyRecords.Add(OrderCreationIdempotencyRecord.Create(customerId, operation, keyHash, requestHash, order.Id, clock.UtcNow)); AddOutbox(integrationEvents);
        try { await db.SaveChangesAsync(ct); order.ClearDomainEvents(); return OrderCreatePersistenceResult.Created; }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear(); OrderIdempotencyResult? duplicate = await FindIdempotencyAsync(customerId, operation, keyHash, ct);
            return duplicate is null ? OrderCreatePersistenceResult.Conflict : duplicate.RequestHash == requestHash ? OrderCreatePersistenceResult.DuplicateSameRequest : OrderCreatePersistenceResult.DuplicateDifferentRequest;
        }
    }

    public Task<Order?> GetForUpdateAsync(OrderId id, CancellationToken ct) => db.Orders.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> SaveAsync(IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken ct)
    {
        AddOutbox(integrationEvents);
        try { await db.SaveChangesAsync(ct); foreach (Order order in db.ChangeTracker.Entries<Order>().Select(x => x.Entity)) order.ClearDomainEvents(); return true; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; }
    }

    public async Task<OrderDetailsResponse?> GetDetailsAsync(Guid orderId, Guid customerId, CancellationToken ct) => Map(await DetailsQuery(customerId).SingleOrDefaultAsync(x => x.Id == new OrderId(orderId), ct));
    public async Task<OrderDetailsResponse?> GetDetailsByNumberAsync(string orderNumber, Guid customerId, CancellationToken ct) => Map(await DetailsQuery(customerId).SingleOrDefaultAsync(x => x.OrderNumber == orderNumber, ct));
    public async Task<OrderListResponse> ListAsync(Guid customerId, int page, int pageSize, CancellationToken ct)
    {
        IQueryable<Order> query = db.Orders.AsNoTracking().Where(x => x.CustomerId == customerId); int total = await query.CountAsync(ct);
        OrderListItemResponse[] items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new OrderListItemResponse(x.Id.Value, x.OrderNumber, (short)x.Type, (short)x.Status, x.Currency, x.TotalMinor, x.Merchant.MerchantDisplayName, x.CreatedAtUtc, x.ScheduledForUtc)).ToArrayAsync(ct);
        return new(items, page, pageSize, total);
    }
    public async Task<IReadOnlyList<OrderTimelineEntryResponse>?> TimelineAsync(Guid orderId, Guid customerId, CancellationToken ct)
    {
        OrderId id = new(orderId); if (!await db.Orders.AsNoTracking().AnyAsync(x => x.Id == id && x.CustomerId == customerId, ct)) return null;
        return await db.OrderStatusHistory.AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.ChangedAtUtc).ThenBy(x => x.Id).Select(x => new OrderTimelineEntryResponse(x.Id.Value, x.PreviousStatus.HasValue ? (short?)x.PreviousStatus.Value : null, (short)x.NewStatus, x.ChangedAtUtc, (short)x.ChangeSource, x.ReasonCode, x.ReasonText, x.CorrelationId)).ToArrayAsync(ct);
    }

    private IQueryable<Order> DetailsQuery(Guid customerId) => db.Orders.AsNoTracking().Where(x => x.CustomerId == customerId).Include(x => x.Items).ThenInclude(x => x.Options).Include(x => x.StatusHistory).AsSplitQuery();
    private void AddOutbox(IEnumerable<IIntegrationEvent> events)
    {
        DateTime now = clock.UtcNow; foreach (IIntegrationEvent integrationEvent in events) db.OutboxMessages.Add(OrderOutboxMessage.Create(new OrderOutboxMessageId(integrationEvent.Id), integrationEvent.GetType().Name, JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions), integrationEvent.OccurredAtUtc, now));
    }
    private static OrderDetailsResponse? Map(Order? x)
    {
        if (x is null) return null;
        OrderItemResponse[] items = x.Items.OrderBy(i => i.Id.Value).Select(i => new OrderItemResponse(i.Id.Value, i.ProductId, i.ProductVersion, i.VariantId, i.ProductName, i.VariantName, i.Sku, i.Quantity, i.UnitBasePriceMinor, i.UnitOptionsPriceMinor, i.UnitDiscountMinor, i.UnitFinalPriceMinor, i.LineSubtotalMinor, i.LineDiscountMinor, i.LineTotalMinor, i.CustomerNote, i.Options.OrderBy(o => o.Id.Value).Select(o => new OrderOptionResponse(o.OptionGroupId, o.OptionId, o.OptionGroupName, o.OptionName, o.Quantity, o.UnitPriceAdjustmentMinor, o.TotalPriceAdjustmentMinor)).ToArray())).ToArray();
        OrderTimelineEntryResponse[] timeline = x.StatusHistory.OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.Id.Value).Select(h => new OrderTimelineEntryResponse(h.Id.Value, h.PreviousStatus.HasValue ? (short?)h.PreviousStatus.Value : null, (short)h.NewStatus, h.ChangedAtUtc, (short)h.ChangeSource, h.ReasonCode, h.ReasonText, h.CorrelationId)).ToArray();
        return new(x.Id.Value, x.OrderNumber, x.SourceCartId, (short)x.Type, (short)x.Status, x.Currency, x.TotalMinor, x.ScheduledForUtc, x.CustomerNotes, x.CancellationCode, x.CancellationReason, x.CancelledBy.HasValue ? (short?)x.CancelledBy.Value : null, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp,
            new(x.Customer.CustomerId, x.Customer.DisplayName, x.Customer.PreferredLanguage),
            new(x.DeliveryAddress.AddressId, x.DeliveryAddress.Label, x.DeliveryAddress.City, x.DeliveryAddress.Area, x.DeliveryAddress.Street, x.DeliveryAddress.BuildingNumber, x.DeliveryAddress.Floor, x.DeliveryAddress.Apartment, x.DeliveryAddress.DeliveryInstructions, x.DeliveryAddress.Latitude, x.DeliveryAddress.Longitude, x.DeliveryAddress.PlaceId, x.DeliveryAddress.FormattedAddress),
            new(x.Merchant.MerchantId, x.Merchant.BranchId, x.Merchant.MerchantDisplayName, x.Merchant.BranchDisplayName, x.Merchant.BranchAddress, x.Merchant.BranchPhoneNumber),
            new(x.SubtotalMinor, x.OptionsTotalMinor, x.ProductDiscountMinor, x.CouponDiscountMinor, x.DeliveryDiscountMinor, x.DeliveryFeeMinor, x.ServiceFeeMinor, x.PlatformFeeMinor, x.SmallOrderFeeMinor, x.TaxMinor, x.TotalMinor, x.Currency, x.PricingReference, x.PricingCalculatedAtUtc), items, timeline);
    }
}
