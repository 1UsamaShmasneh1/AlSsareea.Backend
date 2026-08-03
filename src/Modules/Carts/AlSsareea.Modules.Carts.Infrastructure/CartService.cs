using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Carts.Application;
using AlSsareea.Modules.Carts.Contracts;
using AlSsareea.Modules.Carts.Domain;
using AlSsareea.Modules.Carts.Infrastructure.Persistence;
using AlSsareea.Modules.Catalog.Contracts;
using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Pricing.Contracts;
using AlSsareea.Modules.Promotions.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Carts.Infrastructure;

internal sealed class CartService(
    CartsDbContext db,
    ICartRepository repository,
    ICartCustomerContextProvider customers,
    IMerchantCatalogScopeProvider merchants,
    ICartCatalogValidationService catalog,
    IPricingCalculator pricing,
    ICartPromotionEvaluator promotions,
    IClock clock,
    IOptions<CartsOptions> options) : ICartService
{
    private CartsOptions Options => options.Value;

    public async Task<CartOperationResult<CartResponse>> GetOrCreateAsync(Guid userId, GetOrCreateActiveCartRequest request, string idempotencyKey, CancellationToken ct)
    {
        CartCustomerContext? customer = await Customer(userId, ct); if (customer is null) return Fail<CartResponse>(CartOperationStatus.NotFound, CartErrorCodes.CustomerNotFound); if (!customer.IsAllowed) return Fail<CartResponse>(CartOperationStatus.Forbidden, CartErrorCodes.CustomerNotAllowed);
        if (!await MerchantAvailable(request.MerchantId, request.BranchId, ct)) return Fail<CartResponse>(CartOperationStatus.Invalid, CartErrorCodes.MerchantUnavailable);
        Cart? existing = await repository.GetActiveAsync(customer.CustomerId, request.MerchantId, request.BranchId, ct);
        if (existing is not null)
        {
            if (!existing.ExpireIfNeeded(clock.UtcNow)) return CartOperation.Success(ToResponse(existing));
            await db.SaveChangesAsync(ct);
        }
        string requestHash = Hash(request); CartOperationResult<CartResponse>? duplicate = await Duplicate<CartResponse>(customer.CustomerId, "cart.create", idempotencyKey, requestHash, ct);
        if (duplicate is not null)
        {
            if (duplicate.Value is null) return duplicate;
            Cart? cart = await repository.GetAsync(new CartId(duplicate.Value.Id), false, ct); return cart is null ? Fail<CartResponse>(CartOperationStatus.Conflict, CartErrorCodes.IdempotencyConflict) : CartOperation.Success(ToResponse(cart));
        }
        Cart created = Cart.Create(CartId.New(), customer.CustomerId, request.MerchantId, request.BranchId, clock.UtcNow, Options.ActiveCartLifetime);
        await repository.AddAsync(created, ct); AddIdempotency(customer.CustomerId, "cart.create", idempotencyKey, requestHash, created.Id.Value);
        try { await db.SaveChangesAsync(ct); return CartOperation.Created(ToResponse(created)); }
        catch (DbUpdateException) { return Fail<CartResponse>(CartOperationStatus.Conflict, CartErrorCodes.IdempotencyConflict); }
    }

    public async Task<CartOperationResult<CartResponse>> GetActiveAsync(Guid userId, Guid merchantId, Guid? branchId, CancellationToken ct)
    {
        CartCustomerContext? customer = await Customer(userId, ct); if (customer is null) return Fail<CartResponse>(CartOperationStatus.NotFound, CartErrorCodes.NotFound);
        Cart? cart = await repository.GetActiveAsync(customer.CustomerId, merchantId, branchId, ct); return await Read(cart, customer.CustomerId, ct);
    }
    public async Task<CartOperationResult<CartResponse>> GetAsync(Guid userId, Guid cartId, CancellationToken ct)
    {
        CartCustomerContext? customer = await Customer(userId, ct); Cart? cart = customer is null ? null : await repository.GetAsync(new CartId(cartId), true, ct); return await Read(cart, customer?.CustomerId ?? Guid.Empty, ct);
    }
    public Task<CartOperationResult<CartResponse>> AddItemAsync(Guid userId, Guid cartId, AddCartItemRequest request, string key, CancellationToken ct) =>
        Mutate(userId, cartId, "item.add", key, request, request.ConcurrencyStamp, async cart =>
        {
            CartCatalogValidationResult validation = await catalog.ValidateAsync(new(cart.MerchantId, cart.BranchId, request.ProductId, request.ProductVariantId, request.SelectedOptions.Select(x => new CartCatalogOptionReference(x.OptionGroupId, x.OptionItemId, x.Quantity)).ToArray(), request.Quantity, null, "ar"), ct);
            if (!validation.IsValid || validation.Snapshot is null) throw new CartApplicationException(validation.BlockingReasonCode ?? CartErrorCodes.ProductUnavailable);
            cart.AddItem(request.ProductId, request.ProductVariantId, request.Quantity, request.CustomerNote, validation.Snapshot.ProductVersion, request.SelectedOptions.Select(x => new CartItemOption(x.OptionGroupId, x.OptionItemId, x.Quantity, validation.Snapshot.ProductVersion)), clock.UtcNow);
        }, ct);
    public Task<CartOperationResult<CartResponse>> UpdateQuantityAsync(Guid userId, Guid cartId, Guid itemId, UpdateCartItemQuantityRequest request, string key, CancellationToken ct) =>
        Mutate(userId, cartId, "item.quantity", key, new { itemId, request }, request.ConcurrencyStamp, cart => { CartItem item = cart.Items.SingleOrDefault(x => x.Id.Value == itemId) ?? throw new CartApplicationException(CartErrorCodes.NotFound); cart.UpdateQuantity(item.Id, request.Quantity, item.CatalogVersion, clock.UtcNow); return Task.CompletedTask; }, ct);
    public Task<CartOperationResult<CartResponse>> RemoveItemAsync(Guid userId, Guid cartId, Guid itemId, CartConcurrencyRequest request, string key, CancellationToken ct) =>
        Mutate(userId, cartId, "item.remove", key, new { itemId, request }, request.ConcurrencyStamp, cart => { cart.RemoveItem(new CartItemId(itemId), clock.UtcNow); return Task.CompletedTask; }, ct);
    public Task<CartOperationResult<CartResponse>> ApplyCouponAsync(Guid userId, Guid cartId, ApplyCartCouponRequest request, string key, CancellationToken ct) =>
        Mutate(userId, cartId, "coupon.apply", key, request, request.ConcurrencyStamp, async cart =>
        {
            (PricingEstimateResponse Price, IReadOnlyList<PromotionLineContext> Lines)? context = await PriceContext(cart, ct);
            if (context is null) throw new CartApplicationException(CartErrorCodes.PricingFailed);
            CouponValidationResponse? validation = await promotions.ValidateCartCouponAsync(new(request.CouponCode, cart.CustomerId, cart.MerchantId, cart.BranchId, context.Value.Price.Breakdown, context.Value.Price.Snapshot, context.Value.Lines, new UsageContext(0, 0, 0, false)), ct);
            if (validation?.IsValid != true) throw new CartApplicationException(CartErrorCodes.CouponInvalid);
            cart.ApplyCoupon(request.CouponCode, clock.UtcNow);
        }, ct);
    public Task<CartOperationResult<CartResponse>> RemoveCouponAsync(Guid userId, Guid cartId, CartConcurrencyRequest request, string key, CancellationToken ct) =>
        Mutate(userId, cartId, "coupon.remove", key, request, request.ConcurrencyStamp, cart => { cart.RemoveCoupon(clock.UtcNow); return Task.CompletedTask; }, ct);
    public Task<CartOperationResult<CartResponse>> ClearAsync(Guid userId, Guid cartId, CartConcurrencyRequest request, string key, CancellationToken ct) =>
        Mutate(userId, cartId, "cart.clear", key, request, request.ConcurrencyStamp, cart => { cart.Clear(clock.UtcNow); return Task.CompletedTask; }, ct);

    public async Task<CartOperationResult<CartCheckoutSummaryResponse>> CheckoutSummaryAsync(Guid userId, Guid cartId, CancellationToken ct)
    {
        CartCustomerContext? customer = await Customer(userId, ct); Cart? cart = customer is null ? null : await repository.GetAsync(new CartId(cartId), true, ct);
        if (cart is null || cart.CustomerId != customer!.CustomerId) return Fail<CartCheckoutSummaryResponse>(CartOperationStatus.NotFound, CartErrorCodes.NotFound);
        DateTime now = clock.UtcNow; List<CartBlockingReason> blocking = []; List<CartCheckoutItemResponse> lines = [];
        if (cart.ExpireIfNeeded(now)) await db.SaveChangesAsync(ct);
        if (cart.Status != CartStatus.Active) blocking.Add(new(cart.Status == CartStatus.Expired ? CartErrorCodes.Expired : CartErrorCodes.NotActive, "Cart is not active."));
        if (!await MerchantAvailable(cart.MerchantId, cart.BranchId, ct)) blocking.Add(new(CartErrorCodes.MerchantUnavailable, "Merchant or branch is unavailable."));
        long subtotal = 0; string? currency = null;
        foreach (CartItem item in cart.Items)
        {
            CartCatalogValidationResult validation = await catalog.ValidateAsync(new(cart.MerchantId, cart.BranchId, item.ProductId, item.ProductVariantId, item.SelectedOptions.Select(x => new CartCatalogOptionReference(x.OptionGroupId, x.OptionItemId, x.Quantity)).ToArray(), item.Quantity, item.CatalogVersion, "ar"), ct);
            ProductSnapshot? snapshot = validation.Snapshot; List<CartBlockingReason> reasons = [];
            if (!validation.IsValid || snapshot is null) reasons.Add(new(validation.BlockingReasonCode ?? CartErrorCodes.ProductUnavailable, "Product configuration is unavailable."));
            if (snapshot is not null && currency is not null && currency != snapshot.Currency) reasons.Add(new(CartErrorCodes.CurrencyMismatch, "Cart items use inconsistent currencies."));
            currency ??= snapshot?.Currency; long unit = snapshot?.TotalPriceMinor ?? 0; long lineSubtotal = checked(unit * item.Quantity); subtotal = checked(subtotal + lineSubtotal);
            long optionsTotal = snapshot?.SelectedOptions.Sum(x => x.PriceAdjustmentMinor) ?? 0;
            CartCheckoutOptionResponse[] optionSnapshots = snapshot?.SelectedOptions.Select(x => new CartCheckoutOptionResponse(x.OptionGroupId, x.OptionId, x.OptionGroupName, x.OptionName, item.SelectedOptions.SingleOrDefault(o => o.OptionItemId == x.OptionId)?.Quantity ?? 1, x.PriceAdjustmentMinor, checked(x.PriceAdjustmentMinor * (item.SelectedOptions.SingleOrDefault(o => o.OptionItemId == x.OptionId)?.Quantity ?? 1)))).ToArray() ?? [];
            lines.Add(new(item.Id.Value, item.ProductId, snapshot?.ProductVersion ?? item.CatalogVersion, snapshot?.LocalizedProductName, snapshot?.Sku, item.ProductVariantId, snapshot?.SelectedVariantName, item.Quantity, snapshot is null ? 0 : snapshot.BasePriceMinor + snapshot.VariantPriceAdjustmentMinor, optionsTotal, unit, lineSubtotal, 0, lineSubtotal, item.CustomerNote, validation.IsValid, validation.HasChanged, optionSnapshots, reasons));
            blocking.AddRange(reasons);
        }
        if (cart.Items.Count == 0) blocking.Add(new(CartErrorCodes.Empty, "Cart is empty."));
        PricingEstimateResponse? price = currency is null ? null : await pricing.EstimateAsync(new(cart.MerchantId, cart.BranchId, null, currency, subtotal, null, now), ct);
        if (currency is not null && price is null) blocking.Add(new(CartErrorCodes.PricingFailed, "Pricing is currently unavailable."));
        PromotionEvaluationResponse? promotion = null;
        if (price is not null)
        {
            promotion = await promotions.EvaluateCartAsync(new(customer.CustomerId, cart.MerchantId, cart.BranchId, price.Breakdown, price.Snapshot, lines.Select(x => new PromotionLineContext(x.ProductId, null, x.LineSubtotalMinor)).ToArray(), cart.CouponCode, new UsageContext(0, 0, 0, false)), ct);
            if (promotion is null) blocking.Add(new(CartErrorCodes.PromotionsFailed, "Promotions could not be evaluated."));
        }
        if (cart.Status == CartStatus.Active && price is not null) { cart.MarkPriced(now); await db.SaveChangesAsync(ct); }
        long discount = promotion?.TotalAdjustmentMinor ?? 0; PricingBreakdownDto? p = price?.Breakdown;
        string? promotionReference = promotion is not null && promotion.Snapshots.Count > 0 ? promotion.Snapshots[0].PromotionVersion.ToString("N") : null;
        return CartOperation.Success(new CartCheckoutSummaryResponse(cart.Id.Value, cart.CustomerId, cart.MerchantId, cart.BranchId, (short)cart.Status, currency, lines, subtotal, p?.DeliveryFeeMinor ?? 0, p?.ServiceFeeMinor ?? 0, p?.TaxMinor ?? 0, (p?.PlatformFeeMinor ?? 0) + (p?.SmallOrderFeeMinor ?? 0), discount, (p?.GrandTotalMinor ?? subtotal) - discount, blocking, blocking.Count == 0, price is null ? null : $"{price.Snapshot.PolicyId:N}:{price.Snapshot.PolicyVersion}", promotionReference, now, cart.ConcurrencyStamp, cart.ExpiresAtUtc));
    }

    private async Task<CartOperationResult<CartResponse>> Mutate(Guid userId, Guid cartId, string operation, string key, object request, Guid expectedStamp, Func<Cart, Task> action, CancellationToken ct)
    {
        CartCustomerContext? customer = await Customer(userId, ct); Cart? cart = customer is null ? null : await repository.GetAsync(new CartId(cartId), true, ct);
        if (cart is null || cart.CustomerId != customer!.CustomerId) return Fail<CartResponse>(CartOperationStatus.NotFound, CartErrorCodes.NotFound);
        string requestHash = Hash(request); CartOperationResult<CartResponse>? duplicate = await Duplicate<CartResponse>(customer.CustomerId, operation, key, requestHash, ct);
        if (duplicate is not null) return duplicate.Value is null ? duplicate : CartOperation.Success(ToResponse(cart));
        if (cart.ConcurrencyStamp != expectedStamp) return Fail<CartResponse>(CartOperationStatus.Conflict, CartErrorCodes.ConcurrencyConflict);
        try { await action(cart); AddIdempotency(customer.CustomerId, operation, key, requestHash, cart.Id.Value); await db.SaveChangesAsync(ct); return CartOperation.Success(ToResponse(cart)); }
        catch (CartApplicationException ex) { return Fail<CartResponse>(CartOperationStatus.Invalid, ex.Code); }
        catch (DomainException ex) { return Fail<CartResponse>(CartOperationStatus.Invalid, ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase) ? CartErrorCodes.Expired : CartErrorCodes.NotActive); }
        catch (DbUpdateConcurrencyException) { return Fail<CartResponse>(CartOperationStatus.Conflict, CartErrorCodes.ConcurrencyConflict); }
        catch (DbUpdateException) { return Fail<CartResponse>(CartOperationStatus.Conflict, CartErrorCodes.IdempotencyConflict); }
    }
    private async Task<CartOperationResult<CartResponse>> Read(Cart? cart, Guid customerId, CancellationToken ct)
    {
        if (cart is null || cart.CustomerId != customerId) return Fail<CartResponse>(CartOperationStatus.NotFound, CartErrorCodes.NotFound);
        if (cart.ExpireIfNeeded(clock.UtcNow)) await db.SaveChangesAsync(ct); return CartOperation.Success(ToResponse(cart));
    }
    private async Task<CartCustomerContext?> Customer(Guid userId, CancellationToken ct) => await customers.GetByUserIdAsync(userId, ct);
    private async Task<bool> MerchantAvailable(Guid merchantId, Guid? branchId, CancellationToken ct)
    {
        MerchantCatalogScope? scope = await merchants.GetScopeAsync(merchantId, Guid.Empty, true, ct); if (scope?.MerchantIsActive != true) return false;
        return !branchId.HasValue || await merchants.IsOperationalBranchAsync(merchantId, branchId.Value, ct);
    }
    private async Task<(PricingEstimateResponse Price, IReadOnlyList<PromotionLineContext> Lines)?> PriceContext(Cart cart, CancellationToken ct)
    {
        List<PromotionLineContext> lines = []; string? currency = null; long subtotal = 0;
        foreach (CartItem item in cart.Items)
        {
            CartCatalogValidationResult validation = await catalog.ValidateAsync(new(cart.MerchantId, cart.BranchId, item.ProductId, item.ProductVariantId, item.SelectedOptions.Select(x => new CartCatalogOptionReference(x.OptionGroupId, x.OptionItemId, x.Quantity)).ToArray(), item.Quantity, item.CatalogVersion, "ar"), ct);
            if (!validation.IsValid || validation.Snapshot is null || currency is not null && currency != validation.Snapshot.Currency) return null;
            currency ??= validation.Snapshot.Currency; long line = checked(validation.Snapshot.TotalPriceMinor * item.Quantity); subtotal = checked(subtotal + line); lines.Add(new(item.ProductId, null, line));
        }
        if (currency is null) return null;
        PricingEstimateResponse? result = await pricing.EstimateAsync(new(cart.MerchantId, cart.BranchId, null, currency, subtotal, null, clock.UtcNow), ct);
        return result is null ? null : (result, lines);
    }
    private async Task<CartOperationResult<T>?> Duplicate<T>(Guid customerId, string operation, string key, string requestHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > Options.MaximumIdempotencyKeyLength) return Fail<T>(CartOperationStatus.Conflict, CartErrorCodes.IdempotencyConflict);
        string keyHash = Hash(key); CartIdempotencyRecord? record = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == customerId && x.Operation == operation && x.KeyHash == keyHash && x.ExpiresAtUtc > clock.UtcNow, ct);
        if (record is null) return null; if (record.RequestHash != requestHash) return Fail<T>(CartOperationStatus.Conflict, CartErrorCodes.IdempotencyConflict);
        if (typeof(T) == typeof(CartResponse) && record.CartId.HasValue) { Cart? cart = await repository.GetAsync(new CartId(record.CartId.Value), false, ct); return cart is null ? Fail<T>(CartOperationStatus.Conflict, CartErrorCodes.IdempotencyConflict) : (CartOperationResult<T>)(object)CartOperation.Success(ToResponse(cart)); }
        return Fail<T>(CartOperationStatus.Conflict, CartErrorCodes.IdempotencyConflict);
    }
    private void AddIdempotency(Guid customerId, string operation, string key, string requestHash, Guid? cartId) { DateTime now = clock.UtcNow; db.IdempotencyRecords.Add(CartIdempotencyRecord.Create(CartIdempotencyRecordId.New(), customerId, operation, Hash(key), requestHash, cartId, now, now.AddHours(24))); }
    private static string Hash(object value) { string input = value is string text ? text : JsonSerializer.Serialize(value); return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input))); }
    private static CartOperationResult<T> Fail<T>(CartOperationStatus status, string code) => CartOperation.Failure<T>(status, code);
    private static CartResponse ToResponse(Cart x) => new(x.Id.Value, x.CustomerId, x.MerchantId, x.BranchId, (short)x.Status, x.CouponCode, x.ExpiresAtUtc, x.LastPricedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp, x.Items.OrderBy(i => i.CreatedAtUtc).Select(i => new CartItemResponse(i.Id.Value, i.ProductId, i.ProductVariantId, i.Quantity, i.CustomerNote, i.CatalogVersion, i.SelectedOptions.Select(o => new CartItemOptionResponse(o.OptionGroupId, o.OptionItemId, o.Quantity, o.CatalogVersion)).ToArray(), i.CreatedAtUtc, i.UpdatedAtUtc)).ToArray());
    private sealed class CartApplicationException(string code) : Exception { public string Code { get; } = code; }
}
