using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Carts.Domain;

public sealed class Cart : AggregateRoot<CartId>
{
    private readonly List<CartItem> _items = [];
    private Cart(CartId id, Guid customerId, Guid merchantId, Guid? branchId, DateTime now, DateTime expiresAtUtc) : base(id)
    {
        if (customerId == Guid.Empty || merchantId == Guid.Empty) throw new DomainException("Customer and merchant identifiers are required.");
        if (now.Kind != DateTimeKind.Utc || expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= now) throw new DomainException("Cart timestamps must be valid UTC values.");
        CustomerId = customerId; MerchantId = merchantId; BranchId = branchId; Status = CartStatus.Active;
        CreatedAtUtc = now; UpdatedAtUtc = now; ExpiresAtUtc = expiresAtUtc; ConcurrencyStamp = Guid.NewGuid();
        RaiseDomainEvent(new CartCreatedDomainEvent(id.Value, now));
    }
    private Cart(CartId id) : base(id) { }
    public Guid CustomerId { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid? BranchId { get; private set; }
    public CartStatus Status { get; private set; }
    public string? CouponCode { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastPricedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();
    public static Cart Create(CartId id, Guid customerId, Guid merchantId, Guid? branchId, DateTime now, TimeSpan lifetime) =>
        lifetime <= TimeSpan.Zero ? throw new DomainException("Cart lifetime must be positive.") : new(id, customerId, merchantId, branchId, now, now.Add(lifetime));
    public bool ExpireIfNeeded(DateTime now)
    {
        RequireUtc(now);
        if (Status != CartStatus.Active || now < ExpiresAtUtc) return false;
        Status = CartStatus.Expired; Touch(now); RaiseDomainEvent(new CartExpiredDomainEvent(Id.Value, now)); return true;
    }
    public CartItem AddItem(Guid productId, Guid? variantId, int quantity, string? note, int catalogVersion, IEnumerable<CartItemOption> options, DateTime now)
    {
        EnsureActive(now); ValidateQuantity(quantity); string? normalizedNote = NormalizeNote(note);
        CartItemOption[] normalizedOptions = options.OrderBy(x => x.OptionGroupId).ThenBy(x => x.OptionItemId).ToArray();
        CartItem? existing = _items.SingleOrDefault(x => x.HasConfiguration(productId, variantId, normalizedNote, normalizedOptions));
        if (existing is not null) { existing.ChangeQuantity(checked(existing.Quantity + quantity), catalogVersion, now); Touch(now); return existing; }
        if (_items.Count >= CartRules.MaximumItems) throw new DomainException("Cart item limit exceeded.");
        CartItem item = CartItem.Create(CartItemId.New(), Id, productId, variantId, quantity, normalizedNote, catalogVersion, normalizedOptions, now);
        _items.Add(item); Touch(now); return item;
    }
    public void UpdateQuantity(CartItemId itemId, int quantity, int catalogVersion, DateTime now) { EnsureActive(now); ValidateQuantity(quantity); Find(itemId).ChangeQuantity(quantity, catalogVersion, now); Touch(now); }
    public void RemoveItem(CartItemId itemId, DateTime now) { EnsureActive(now); if (!_items.Remove(Find(itemId))) throw new DomainException("Cart item was not found."); Touch(now); }
    public void ApplyCoupon(string code, DateTime now) { EnsureActive(now); string value = code.Trim().ToUpperInvariant(); if (value.Length is 0 or > CartRules.MaximumCouponLength) throw new DomainException("Coupon code is invalid."); CouponCode = value; Touch(now); }
    public void RemoveCoupon(DateTime now) { EnsureActive(now); CouponCode = null; Touch(now); }
    public void Clear(DateTime now) { EnsureActive(now); Status = CartStatus.Cleared; CouponCode = null; Touch(now); RaiseDomainEvent(new CartClearedDomainEvent(Id.Value, now)); }
    public void MarkPriced(DateTime now) { EnsureActive(now); LastPricedAtUtc = now; Touch(now); }
    private CartItem Find(CartItemId id) => _items.SingleOrDefault(x => x.Id == id) ?? throw new DomainException("Cart item was not found.");
    private void EnsureActive(DateTime now) { ExpireIfNeeded(now); if (Status != CartStatus.Active) throw new DomainException(Status == CartStatus.Expired ? "Cart has expired." : "Cart is not active."); }
    private void Touch(DateTime now) { RequireUtc(now); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    private static void ValidateQuantity(int value) { if (value is < 1 or > CartRules.MaximumQuantity) throw new DomainException("Quantity is outside the supported range."); }
    private static string? NormalizeNote(string? value) { string? result = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); if (result?.Length > CartRules.MaximumNoteLength) throw new DomainException("Item note is too long."); return result; }
    private static void RequireUtc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new DomainException("Timestamp must be UTC."); }
}

public sealed class CartItem : Entity<CartItemId>
{
    private readonly List<CartItemOption> _selectedOptions = [];
    private CartItem(CartItemId id, CartId cartId, Guid productId, Guid? variantId, int quantity, string? note, int catalogVersion, DateTime now) : base(id)
    { CartId = cartId; ProductId = productId; ProductVariantId = variantId; Quantity = quantity; CustomerNote = note; CatalogVersion = catalogVersion; CreatedAtUtc = now; UpdatedAtUtc = now; }
    private CartItem(CartItemId id) : base(id) { }
    public CartId CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? ProductVariantId { get; private set; }
    public int Quantity { get; private set; }
    public string? CustomerNote { get; private set; }
    public int CatalogVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<CartItemOption> SelectedOptions => _selectedOptions.AsReadOnly();
    internal static CartItem Create(CartItemId id, CartId cartId, Guid productId, Guid? variantId, int quantity, string? note, int version, IEnumerable<CartItemOption> options, DateTime now)
    {
        if (productId == Guid.Empty || version < 1) throw new DomainException("Product reference is invalid.");
        CartItem item = new(id, cartId, productId, variantId, quantity, note, version, now); item._selectedOptions.AddRange(options); return item;
    }
    internal bool HasConfiguration(Guid productId, Guid? variantId, string? note, IReadOnlyList<CartItemOption> options) =>
        ProductId == productId && ProductVariantId == variantId && CustomerNote == note && _selectedOptions.SequenceEqual(options);
    internal void ChangeQuantity(int quantity, int version, DateTime now) { if (quantity > CartRules.MaximumQuantity) throw new DomainException("Quantity is outside the supported range."); Quantity = quantity; CatalogVersion = version; UpdatedAtUtc = now; }
}

public sealed record CartItemOption
{
    public CartItemOption(Guid optionGroupId, Guid optionItemId, int quantity, int catalogVersion)
    {
        if (optionGroupId == Guid.Empty || optionItemId == Guid.Empty || quantity < 1 || catalogVersion < 1) throw new DomainException("Selected option reference is invalid.");
        OptionGroupId = optionGroupId; OptionItemId = optionItemId; Quantity = quantity; CatalogVersion = catalogVersion;
    }
    public Guid OptionGroupId { get; init; }
    public Guid OptionItemId { get; init; }
    public int Quantity { get; init; }
    public int CatalogVersion { get; init; }
}

public sealed class CartIdempotencyRecord : Entity<CartIdempotencyRecordId>
{
    private CartIdempotencyRecord(CartIdempotencyRecordId id, Guid customerId, string operation, string keyHash, string requestHash, Guid? cartId, DateTime createdAtUtc, DateTime expiresAtUtc) : base(id)
    { CustomerId = customerId; Operation = operation; KeyHash = keyHash; RequestHash = requestHash; CartId = cartId; CreatedAtUtc = createdAtUtc; ExpiresAtUtc = expiresAtUtc; }
    private CartIdempotencyRecord(CartIdempotencyRecordId id) : base(id) { }
    public Guid CustomerId { get; private set; }
    public string Operation { get; private set; } = string.Empty; public string KeyHash { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty; public Guid? CartId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public static CartIdempotencyRecord Create(CartIdempotencyRecordId id, Guid customerId, string operation, string keyHash, string requestHash, Guid? cartId, DateTime now, DateTime expires) =>
        new(id, customerId, operation, keyHash, requestHash, cartId, now, expires);
}
