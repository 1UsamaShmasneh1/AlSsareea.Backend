using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;

namespace AlSsareea.Modules.Orders.Application;

public static class OrderErrorCodes
{
    public const string NotFound = "orders.order_not_found";
    public const string InvalidRequest = "orders.invalid_request";
    public const string InvalidCart = "orders.invalid_cart";
    public const string InvalidAddress = "orders.invalid_address";
    public const string MerchantUnavailable = "orders.merchant_unavailable";
    public const string IdempotencyConflict = "orders.idempotency_conflict";
    public const string ConcurrencyConflict = "orders.concurrency_conflict";
    public const string InvalidTransition = "orders.invalid_transition";
    public const string Forbidden = "orders.forbidden";
}
public enum OrderOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden, Unprocessable }
public sealed record OrderOperationResult<T>(OrderOperationStatus Status, T? Value = default, string? ErrorCode = null);
public enum OrderCreatePersistenceResult { Created, DuplicateSameRequest, DuplicateDifferentRequest, Conflict }
public sealed record OrderIdempotencyResult(Guid OrderId, string RequestHash);

public interface IOrderRepository
{
    Task<OrderIdempotencyResult?> FindIdempotencyAsync(Guid customerId, string operation, string keyHash, CancellationToken ct);
    Task<OrderCreatePersistenceResult> CreateAsync(Order order, Guid customerId, string operation, string keyHash, string requestHash, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken ct);
    Task<Order?> GetForUpdateAsync(OrderId id, CancellationToken ct);
    Task<bool> SaveAsync(IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken ct);
    Task<OrderDetailsResponse?> GetDetailsAsync(Guid orderId, Guid customerId, CancellationToken ct);
    Task<OrderDetailsResponse?> GetDetailsByNumberAsync(string orderNumber, Guid customerId, CancellationToken ct);
    Task<OrderListResponse> ListAsync(Guid customerId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<OrderTimelineEntryResponse>?> TimelineAsync(Guid orderId, Guid customerId, CancellationToken ct);
}

public interface IOrderService
{
    Task<OrderOperationResult<CreateOrderResponse>> CreateAsync(Guid userId, CreateOrderRequest request, string idempotencyKey, CancellationToken ct);
    Task<OrderOperationResult<OrderDetailsResponse>> GetAsync(Guid userId, Guid orderId, CancellationToken ct);
    Task<OrderOperationResult<OrderDetailsResponse>> GetByNumberAsync(Guid userId, string orderNumber, CancellationToken ct);
    Task<OrderOperationResult<OrderListResponse>> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct);
    Task<OrderOperationResult<IReadOnlyList<OrderTimelineEntryResponse>>> TimelineAsync(Guid userId, Guid orderId, CancellationToken ct);
    Task<OrderOperationResult<OrderDetailsResponse>> CancelAsync(Guid userId, Guid orderId, CancelOrderRequest request, string? correlationId, CancellationToken ct);
}
