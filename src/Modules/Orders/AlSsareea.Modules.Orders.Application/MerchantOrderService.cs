using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;

namespace AlSsareea.Modules.Orders.Application;

public sealed partial class MerchantOrderService(
    IOrderRepository repository,
    IMerchantOrderOperationsScopeProvider merchantScopes,
    IEnumerable<IMerchantOrderRealtimePublisher> realtimePublishers,
    IClock clock) : IMerchantOrderService
{
    public async Task<OrderOperationResult<PagedMerchantOrdersResponse>> ListAsync(MerchantOrderActor actor, MerchantOrderQueryParameters query, CancellationToken ct)
    {
        query = Normalize(query);
        if (!ValidActor(actor) || !ValidQuery(query)) return Fail<PagedMerchantOrdersResponse>(OrderOperationStatus.Invalid, OrderErrorCodes.InvalidRequest);
        IReadOnlyList<MerchantOrderOperationsScope> scopes = await merchantScopes.GetScopesAsync(actor.UserId, ct);
        if (scopes.Count == 0 || query.BranchId.HasValue && !CanAccessBranch(scopes, query.BranchId.Value)) return Fail<PagedMerchantOrdersResponse>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        return Success(await repository.ListMerchantAsync(scopes, query, ct));
    }

    public async Task<OrderOperationResult<MerchantOrderDetails>> GetAsync(MerchantOrderActor actor, Guid orderId, CancellationToken ct)
    {
        if (!ValidActor(actor) || orderId == Guid.Empty) return Fail<MerchantOrderDetails>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        IReadOnlyList<MerchantOrderOperationsScope> scopes = await merchantScopes.GetScopesAsync(actor.UserId, ct);
        MerchantOrderDetails? value = await repository.GetMerchantDetailsAsync(orderId, scopes, ct);
        return value is null ? Fail<MerchantOrderDetails>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound) : Success(value);
    }

    public async Task<OrderOperationResult<IReadOnlyList<MerchantOrderStatusHistoryEntry>>> HistoryAsync(MerchantOrderActor actor, Guid orderId, CancellationToken ct)
    {
        OrderOperationResult<MerchantOrderDetails> result = await GetAsync(actor, orderId, ct);
        return result.Value is null
            ? Fail<IReadOnlyList<MerchantOrderStatusHistoryEntry>>(result.Status, result.ErrorCode ?? OrderErrorCodes.NotFound)
            : Success<IReadOnlyList<MerchantOrderStatusHistoryEntry>>(result.Value.Timeline);
    }

    public Task<OrderOperationResult<MerchantOrderDetails>> AcceptAsync(MerchantOrderActor actor, Guid orderId, AcceptMerchantOrderRequest request, string idempotencyKey, CancellationToken ct) =>
        ValidPreparationMinutes(request.PreparationMinutes)
            ? Mutate(actor, orderId, request.ConcurrencyStamp, request, idempotencyKey, "merchant.accept", "order.accepted",
                (order, now) => order.AcceptByMerchant(request.PreparationMinutes, now, actor.UserId, actor.CorrelationId), null, true, ct)
            : Task.FromResult(Fail<MerchantOrderDetails>(OrderOperationStatus.Invalid, OrderErrorCodes.PreparationTimeInvalid));

    public Task<OrderOperationResult<MerchantOrderDetails>> RejectAsync(MerchantOrderActor actor, Guid orderId, RejectMerchantOrderRequest request, string idempotencyKey, CancellationToken ct)
    {
        MerchantOrderRejectionReason reason = (MerchantOrderRejectionReason)request.ReasonCode;
        if (!Enum.IsDefined(reason) || reason == 0 || request.Note?.Length > OrderRules.ReasonTextMaximumLength || reason == MerchantOrderRejectionReason.Other && string.IsNullOrWhiteSpace(request.Note))
            return Task.FromResult(Fail<MerchantOrderDetails>(OrderOperationStatus.Invalid, OrderErrorCodes.RejectionReasonInvalid));
        return Mutate(actor, orderId, request.ConcurrencyStamp, request, idempotencyKey, "merchant.reject", "order.rejected",
            (order, now) => order.RejectByMerchant(reason, request.Note, now, actor.UserId, actor.CorrelationId), reason.ToString(), false, ct);
    }

    public Task<OrderOperationResult<MerchantOrderDetails>> UpdatePreparationTimeAsync(MerchantOrderActor actor, Guid orderId, UpdatePreparationTimeRequest request, string idempotencyKey, CancellationToken ct) =>
        ValidPreparationMinutes(request.PreparationMinutes)
            ? Mutate(actor, orderId, request.ConcurrencyStamp, request, idempotencyKey, "merchant.preparation-time", "order.preparation-time-updated",
                (order, now) => order.UpdatePreparationTime(request.PreparationMinutes, now, actor.UserId, actor.CorrelationId), null, false, ct)
            : Task.FromResult(Fail<MerchantOrderDetails>(OrderOperationStatus.Invalid, OrderErrorCodes.PreparationTimeInvalid));

    public Task<OrderOperationResult<MerchantOrderDetails>> StartPreparationAsync(MerchantOrderActor actor, Guid orderId, MerchantOrderTransitionRequest request, string idempotencyKey, CancellationToken ct) =>
        Mutate(actor, orderId, request.ConcurrencyStamp, request, idempotencyKey, "merchant.start-preparation", "order.preparing",
            (order, now) => order.StartPreparing(now, actor.UserId, actor.CorrelationId), null, false, ct);

    public Task<OrderOperationResult<MerchantOrderDetails>> MarkReadyAsync(MerchantOrderActor actor, Guid orderId, MerchantOrderTransitionRequest request, string idempotencyKey, CancellationToken ct) =>
        Mutate(actor, orderId, request.ConcurrencyStamp, request, idempotencyKey, "merchant.mark-ready", "order.ready-for-pickup",
            (order, now) => order.MarkReadyForPickup(now, actor.UserId, actor.CorrelationId), null, false, ct);

    private async Task<OrderOperationResult<MerchantOrderDetails>> Mutate<TRequest>(
        MerchantOrderActor actor,
        Guid orderId,
        Guid concurrencyStamp,
        TRequest request,
        string idempotencyKey,
        string operation,
        string eventName,
        Action<Order, DateTime> mutation,
        string? safeReasonCode,
        bool requireOperational,
        CancellationToken ct)
    {
        if (!ValidActor(actor) || orderId == Guid.Empty || concurrencyStamp == Guid.Empty || !ValidKey(idempotencyKey)) return Fail<MerchantOrderDetails>(OrderOperationStatus.Invalid, OrderErrorCodes.InvalidRequest);

        Order? order = await repository.GetForUpdateAsync(new OrderId(orderId), ct);
        if (order is null) return Fail<MerchantOrderDetails>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        MerchantOrderOperationsScope? scope = await merchantScopes.GetScopeAsync(order.MerchantId, actor.UserId, ct);
        if (scope is null || !ScopeIncludes(scope, order.MerchantBranchId)) return Fail<MerchantOrderDetails>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound);
        if (requireOperational && !scope.MerchantIsActive) return Fail<MerchantOrderDetails>(OrderOperationStatus.Conflict, OrderErrorCodes.MerchantUnavailable);
        if (requireOperational && order.MerchantBranchId.HasValue && !await merchantScopes.IsOperationalBranchAsync(order.MerchantId, order.MerchantBranchId.Value, ct)) return Fail<MerchantOrderDetails>(OrderOperationStatus.Conflict, OrderErrorCodes.BranchInactive);

        string keyHash = Hash(idempotencyKey);
        string requestHash = Hash(request!);
        OrderIdempotencyResult? existing = await repository.FindIdempotencyAsync(actor.UserId, operation, keyHash, ct);
        if (existing is not null)
        {
            if (existing.OrderId != orderId || existing.RequestHash != requestHash) return Fail<MerchantOrderDetails>(OrderOperationStatus.Conflict, OrderErrorCodes.IdempotencyConflict);
            return await Existing(orderId, scope, ct);
        }
        if (order.ConcurrencyStamp != concurrencyStamp) return Fail<MerchantOrderDetails>(OrderOperationStatus.Conflict, OrderErrorCodes.ConcurrencyConflict);

        OrderStatus previous = order.Status;
        DateTime now = clock.UtcNow;
        try { mutation(order, now); }
        catch (DomainException) { return Fail<MerchantOrderDetails>(OrderOperationStatus.Conflict, OrderErrorCodes.InvalidTransition); }
        catch (ArgumentOutOfRangeException) { return Fail<MerchantOrderDetails>(OrderOperationStatus.Invalid, OrderErrorCodes.InvalidRequest); }

        MerchantOrderChangedIntegrationEvent integrationEvent = new(
            Guid.NewGuid(), 1, order.Id.Value, order.OrderNumber, order.MerchantId, order.MerchantBranchId,
            operation, (short)previous, (short)order.Status, actor.UserId,
            order.EstimatedPreparationMinutes, order.EstimatedReadyAtUtc, now);
        MerchantOrderAuditEntry audit = new(
            actor.UserId, order.MerchantId, order.MerchantBranchId, order.Id.Value, operation,
            previous, order.Status, now, NormalizeCorrelation(actor.CorrelationId), keyHash, safeReasonCode);
        MerchantOrderPersistenceResult saved = await repository.SaveMerchantOperationAsync(
            order, actor.UserId, operation, keyHash, requestHash, audit, [integrationEvent], ct);
        if (saved == MerchantOrderPersistenceResult.DuplicateDifferentRequest) return Fail<MerchantOrderDetails>(OrderOperationStatus.Conflict, OrderErrorCodes.IdempotencyConflict);
        if (saved == MerchantOrderPersistenceResult.ConcurrencyConflict) return Fail<MerchantOrderDetails>(OrderOperationStatus.Conflict, OrderErrorCodes.ConcurrencyConflict);
        if (saved == MerchantOrderPersistenceResult.Conflict) return Fail<MerchantOrderDetails>(OrderOperationStatus.Conflict, OrderErrorCodes.InvalidTransition);
        if (saved == MerchantOrderPersistenceResult.DuplicateSameRequest) return await Existing(orderId, scope, ct);

        MerchantOrderRealtimeEvent notification = new(
            eventName, order.Id.Value, order.OrderNumber, order.MerchantId, order.MerchantBranchId,
            (short)order.Status, order.UpdatedAtUtc, order.EstimatedPreparationMinutes, order.EstimatedReadyAtUtc);
        foreach (IMerchantOrderRealtimePublisher publisher in realtimePublishers) await publisher.PublishAsync(notification, ct);
        return await Existing(orderId, scope, ct);
    }

    private async Task<OrderOperationResult<MerchantOrderDetails>> Existing(Guid orderId, MerchantOrderOperationsScope scope, CancellationToken ct)
    {
        MerchantOrderDetails? details = await repository.GetMerchantDetailsAsync(orderId, [scope], ct);
        return details is null ? Fail<MerchantOrderDetails>(OrderOperationStatus.NotFound, OrderErrorCodes.NotFound) : Success(details);
    }

    private static bool ValidActor(MerchantOrderActor actor) => actor.UserId != Guid.Empty;
    private static MerchantOrderQueryParameters Normalize(MerchantOrderQueryParameters query) => new()
    {
        BranchId = query.BranchId,
        Status = query.Status,
        OrderType = query.OrderType,
        FromUtc = query.FromUtc,
        ToUtc = query.ToUtc,
        UpdatedSinceUtc = query.UpdatedSinceUtc,
        Search = query.Search,
        Bucket = string.IsNullOrWhiteSpace(query.Bucket) ? "active" : query.Bucket,
        Scheduled = query.Scheduled,
        SortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "submittedAt" : query.SortBy,
        SortDescending = query.SortDescending ?? true,
        Page = query.Page is null or 0 ? 1 : query.Page,
        PageSize = query.PageSize is null or 0 ? 20 : query.PageSize,
    };
    private static bool ValidPreparationMinutes(int value) => value is >= OrderRules.PreparationMinutesMinimum and <= OrderRules.PreparationMinutesMaximum;
    private static bool ScopeIncludes(MerchantOrderOperationsScope scope, Guid? branchId) => !scope.RestrictedBranchId.HasValue || scope.RestrictedBranchId == branchId;
    private static bool CanAccessBranch(IEnumerable<MerchantOrderOperationsScope> scopes, Guid branchId) => scopes.Any(x => !x.RestrictedBranchId.HasValue || x.RestrictedBranchId == branchId);
    private static bool ValidQuery(MerchantOrderQueryParameters query) =>
        query.Page >= 1 && query.PageSize is >= 1 and <= 100 &&
        (query.Search is null || query.Search.Length <= 100) &&
        query.Bucket is "new" or "active" or "history" or "all" &&
        query.SortBy is "submittedAt" or "updatedAt" or "scheduledFor" or "orderNumber" &&
        (!query.Status.HasValue || Enum.IsDefined((OrderStatus)query.Status.Value)) &&
        (!query.OrderType.HasValue || Enum.IsDefined((OrderType)query.OrderType.Value)) &&
        (!query.FromUtc.HasValue || query.FromUtc.Value.Kind == DateTimeKind.Utc) &&
        (!query.ToUtc.HasValue || query.ToUtc.Value.Kind == DateTimeKind.Utc) &&
        (!query.UpdatedSinceUtc.HasValue || query.UpdatedSinceUtc.Value.Kind == DateTimeKind.Utc) &&
        (!query.FromUtc.HasValue || !query.ToUtc.HasValue || query.FromUtc <= query.ToUtc);
    private static bool ValidKey(string key) => key.Length is >= 8 and <= OrderRules.IdempotencyKeyMaximumLength && IdempotencyKeyRegex().IsMatch(key);
    private static string Hash(object value) { string text = value is string s ? s : JsonSerializer.Serialize(value); return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))); }
    private static string? NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static OrderOperationResult<T> Success<T>(T value) => new(OrderOperationStatus.Success, value);
    private static OrderOperationResult<T> Fail<T>(OrderOperationStatus status, string code) => new(status, default, code);
    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)] private static partial Regex IdempotencyKeyRegex();
}
