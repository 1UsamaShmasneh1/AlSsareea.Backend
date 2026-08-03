using System.Linq.Expressions;
using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Orders.Infrastructure.Persistence;

internal sealed class OrderRepository(OrdersDbContext db, IClock clock) : IOrderRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OrderIdempotencyResult?> FindIdempotencyAsync(Guid actorId, string operation, string keyHash, CancellationToken ct) =>
        await db.IdempotencyRecords.AsNoTracking().Where(x => x.ActorId == actorId && x.Operation == operation && x.KeyHash == keyHash).Select(x => new OrderIdempotencyResult(x.OrderId.Value, x.RequestHash)).SingleOrDefaultAsync(ct);

    public async Task<OrderCreatePersistenceResult> CreateAsync(Order order, Guid customerId, string operation, string keyHash, string requestHash, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken ct)
    {
        db.Orders.Add(order); db.IdempotencyRecords.Add(OrderOperationIdempotencyRecord.Create(customerId, operation, keyHash, requestHash, order.Id, clock.UtcNow)); AddOutbox(integrationEvents);
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

    public async Task<MerchantOrderPersistenceResult> SaveMerchantOperationAsync(Order order, Guid actorUserId, string operation, string keyHash, string requestHash, MerchantOrderAuditEntry audit, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken ct)
    {
        db.IdempotencyRecords.Add(OrderOperationIdempotencyRecord.Create(actorUserId, operation, keyHash, requestHash, order.Id, clock.UtcNow));
        db.MerchantOrderAudit.Add(MerchantOrderAuditRecord.Create(audit));
        AddOutbox(integrationEvents);
        try
        {
            await db.SaveChangesAsync(ct);
            order.ClearDomainEvents();
            return MerchantOrderPersistenceResult.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return MerchantOrderPersistenceResult.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            OrderIdempotencyResult? duplicate = await FindIdempotencyAsync(actorUserId, operation, keyHash, ct);
            if (duplicate is null) return MerchantOrderPersistenceResult.Conflict;
            return duplicate.OrderId == order.Id.Value && duplicate.RequestHash == requestHash
                ? MerchantOrderPersistenceResult.DuplicateSameRequest
                : MerchantOrderPersistenceResult.DuplicateDifferentRequest;
        }
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

    public async Task<PagedMerchantOrdersResponse> ListMerchantAsync(IReadOnlyCollection<MerchantOrderOperationsScope> scopes, MerchantOrderQueryParameters query, CancellationToken ct)
    {
        IQueryable<Order> orders = db.Orders.AsNoTracking().Where(ScopePredicate(scopes)).Where(x => x.SubmittedAtUtc != null);
        orders = query.Bucket switch
        {
            "new" => orders.Where(x => x.Status == OrderStatus.Submitted),
            "active" => orders.Where(x => x.Status == OrderStatus.AcceptedByMerchant || x.Status == OrderStatus.Preparing || x.Status == OrderStatus.ReadyForPickup),
            "history" => orders.Where(x => x.Status != OrderStatus.Submitted && x.Status != OrderStatus.AcceptedByMerchant && x.Status != OrderStatus.Preparing && x.Status != OrderStatus.ReadyForPickup),
            _ => orders,
        };
        if (query.BranchId.HasValue) orders = orders.Where(x => x.MerchantBranchId == query.BranchId);
        if (query.Status.HasValue) { OrderStatus status = (OrderStatus)query.Status.Value; orders = orders.Where(x => x.Status == status); }
        if (query.OrderType.HasValue) { OrderType type = (OrderType)query.OrderType.Value; orders = orders.Where(x => x.Type == type); }
        if (query.FromUtc.HasValue) orders = orders.Where(x => x.SubmittedAtUtc >= query.FromUtc);
        if (query.ToUtc.HasValue) orders = orders.Where(x => x.SubmittedAtUtc <= query.ToUtc);
        if (query.UpdatedSinceUtc.HasValue) orders = orders.Where(x => x.UpdatedAtUtc > query.UpdatedSinceUtc);
        if (query.Scheduled.HasValue) orders = query.Scheduled.Value ? orders.Where(x => x.ScheduledForUtc != null) : orders.Where(x => x.ScheduledForUtc == null);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string prefix = query.Search.Trim().ToUpperInvariant() + "%";
            orders = orders.Where(x => EF.Functions.ILike(x.OrderNumber, prefix));
        }

        int total = await orders.CountAsync(ct);
        IOrderedQueryable<Order> ordered = (query.SortBy, query.SortDescending) switch
        {
            ("updatedAt", true) => orders.OrderByDescending(x => x.UpdatedAtUtc),
            ("updatedAt", false) => orders.OrderBy(x => x.UpdatedAtUtc),
            ("scheduledFor", true) => orders.OrderByDescending(x => x.ScheduledForUtc),
            ("scheduledFor", false) => orders.OrderBy(x => x.ScheduledForUtc),
            ("orderNumber", true) => orders.OrderByDescending(x => x.OrderNumber),
            ("orderNumber", false) => orders.OrderBy(x => x.OrderNumber),
            (_, true) => orders.OrderByDescending(x => x.SubmittedAtUtc),
            _ => orders.OrderBy(x => x.SubmittedAtUtc),
        };
        int page = query.Page ?? 1;
        int pageSize = query.PageSize ?? 20;
        MerchantOrderSummary[] items = await ordered.ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new MerchantOrderSummary(
                x.Id.Value, x.OrderNumber, x.MerchantId, x.MerchantBranchId, (short)x.Type, (short)x.Status,
                x.Customer.DisplayName, x.Items.Count, x.SubtotalMinor, x.TotalMinor, x.Currency,
                x.SubmittedAtUtc, x.ScheduledForUtc, x.UpdatedAtUtc, x.EstimatedPreparationMinutes,
                x.EstimatedReadyAtUtc, x.ConcurrencyStamp))
            .ToArrayAsync(ct);
        return new(items, page, pageSize, total, items.Length == 0 ? null : items.Max(x => x.UpdatedAtUtc));
    }

    public async Task<MerchantOrderDetails?> GetMerchantDetailsAsync(Guid orderId, IReadOnlyCollection<MerchantOrderOperationsScope> scopes, CancellationToken ct)
    {
        if (scopes.Count == 0) return null;
        OrderId id = new(orderId);
        Order? order = await db.Orders.AsNoTracking().Where(ScopePredicate(scopes)).Where(x => x.SubmittedAtUtc != null)
            .Include(x => x.Items).ThenInclude(x => x.Options).Include(x => x.StatusHistory).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return MapMerchant(order);
    }

    private IQueryable<Order> DetailsQuery(Guid customerId) => db.Orders.AsNoTracking().Where(x => x.CustomerId == customerId).Include(x => x.Items).ThenInclude(x => x.Options).Include(x => x.StatusHistory).AsSplitQuery();
    private static Expression<Func<Order, bool>> ScopePredicate(IEnumerable<MerchantOrderOperationsScope> scopes)
    {
        ParameterExpression order = Expression.Parameter(typeof(Order), "order");
        Expression body = Expression.Constant(false);
        foreach (MerchantOrderOperationsScope scope in scopes)
        {
            Expression merchant = Expression.Equal(Expression.Property(order, nameof(Order.MerchantId)), Expression.Constant(scope.MerchantId));
            Expression branch = scope.RestrictedBranchId.HasValue
                ? Expression.Equal(Expression.Property(order, nameof(Order.MerchantBranchId)), Expression.Constant(scope.RestrictedBranchId, typeof(Guid?)))
                : Expression.Constant(true);
            body = Expression.OrElse(body, Expression.AndAlso(merchant, branch));
        }
        return Expression.Lambda<Func<Order, bool>>(body, order);
    }
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

    private static MerchantOrderDetails? MapMerchant(Order? x)
    {
        if (x is null) return null;
        MerchantOrderItem[] items = x.Items.OrderBy(i => i.Id.Value).Select(i => new MerchantOrderItem(
            i.Id.Value, i.ProductId, i.ProductName, i.VariantName, i.Sku, i.Quantity, i.UnitFinalPriceMinor,
            i.LineTotalMinor, i.CustomerNote, i.Options.OrderBy(o => o.Id.Value).Select(o => new MerchantOrderOption(
                o.OptionGroupId, o.OptionId, o.OptionGroupName, o.OptionName, o.Quantity,
                o.UnitPriceAdjustmentMinor, o.TotalPriceAdjustmentMinor)).ToArray())).ToArray();
        MerchantOrderStatusHistoryEntry[] timeline = x.StatusHistory.OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.Id.Value)
            .Select(h => new MerchantOrderStatusHistoryEntry(h.Id.Value,
                h.PreviousStatus.HasValue ? (short?)h.PreviousStatus.Value : null, (short)h.NewStatus,
                h.ChangedAtUtc, (short)h.ChangeSource, h.ReasonCode, h.ReasonText)).ToArray();
        return new MerchantOrderDetails(
            x.Id.Value, x.OrderNumber, x.MerchantId, x.MerchantBranchId, (short)x.Type, (short)x.Status,
            x.Customer.DisplayName, x.Customer.PreferredLanguage, x.DeliveryAddress.City, x.DeliveryAddress.Area,
            x.DeliveryAddress.Street, x.DeliveryAddress.BuildingNumber, x.DeliveryAddress.DeliveryInstructions,
            x.CustomerNotes, x.MerchantNotes, x.SubtotalMinor, x.ProductDiscountMinor, x.CouponDiscountMinor,
            x.DeliveryDiscountMinor, x.TaxMinor, x.TotalMinor, x.Currency, x.SubmittedAtUtc, x.ScheduledForUtc,
            x.UpdatedAtUtc, x.AcceptedAtUtc, x.RejectedAtUtc, x.PreparingAtUtc, x.ReadyForPickupAtUtc,
            x.EstimatedPreparationMinutes, x.EstimatedReadyAtUtc,
            x.MerchantRejectionReason.HasValue ? (short?)x.MerchantRejectionReason.Value : null,
            x.MerchantRejectionNote, x.ConcurrencyStamp, items, timeline);
    }
}
