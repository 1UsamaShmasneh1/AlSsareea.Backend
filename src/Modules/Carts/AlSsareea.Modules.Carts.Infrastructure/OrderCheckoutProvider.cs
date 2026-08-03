using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Carts.Application;
using AlSsareea.Modules.Carts.Contracts;
using AlSsareea.Modules.Carts.Domain;
using AlSsareea.Modules.Carts.Infrastructure.Persistence;
using AlSsareea.Modules.Customers.Contracts;

namespace AlSsareea.Modules.Carts.Infrastructure;

internal sealed class OrderCheckoutProvider(ICartService service, ICartRepository repository, ICartCustomerContextProvider customers, CartsDbContext db, IClock clock) : IOrderCheckoutProvider
{
    public async Task<OrderCheckoutResult> GetTrustedSummaryAsync(Guid userId, Guid cartId, Guid? expectedConcurrencyStamp, CancellationToken cancellationToken = default)
    {
        CartOperationResult<CartCheckoutSummaryResponse> result = await service.CheckoutSummaryAsync(userId, cartId, cancellationToken);
        if (result.Value is null) return new(result.Status == CartOperationStatus.NotFound ? OrderCheckoutStatus.NotFound : OrderCheckoutStatus.Invalid, null, result.ErrorCode);
        if (expectedConcurrencyStamp.HasValue && result.Value.ConcurrencyStamp != expectedConcurrencyStamp.Value) return new(OrderCheckoutStatus.Conflict, null, CartErrorCodes.ConcurrencyConflict);
        string error = result.Value.BlockingReasons.Count == 0 ? CartErrorCodes.NotActive : result.Value.BlockingReasons[0].Code;
        return result.Value.IsCheckoutReady ? new(OrderCheckoutStatus.Success, result.Value) : new(OrderCheckoutStatus.Invalid, null, error);
    }

    public async Task<bool> MarkConvertedAsync(Guid userId, Guid cartId, Guid orderId, CancellationToken cancellationToken = default)
    {
        Cart? cart = await repository.GetAsync(new CartId(cartId), true, cancellationToken);
        CartCustomerContext? customer = await customers.GetByUserIdAsync(userId, cancellationToken);
        if (cart is null || customer is null || cart.CustomerId != customer.CustomerId) return false;
        try { cart.MarkConverted(orderId, clock.UtcNow); await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DomainException) { return cart.Status == CartStatus.Converted; }
    }
}
