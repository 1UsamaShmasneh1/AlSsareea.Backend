using AlSsareea.Modules.Carts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Carts.Infrastructure.Persistence;

internal static class CartConfigurationExtensions
{
    internal static PropertyBuilder<T> StrongId<T>(this PropertyBuilder<T> property, Func<T, Guid> toGuid, Func<Guid, T> fromGuid) =>
        property.HasConversion(value => toGuid(value), value => fromGuid(value)).HasColumnType("uuid").ValueGeneratedNever();
}
internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.ToTable("carts", CartsPersistence.Schema); b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new CartId(x));
        b.Property(x => x.CustomerId).HasColumnType("uuid"); b.Property(x => x.MerchantId).HasColumnType("uuid"); b.Property(x => x.BranchId).HasColumnType("uuid");
        b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.CouponCode).HasMaxLength(CartRules.MaximumCouponLength);
        b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => new { x.CustomerId, x.MerchantId, x.BranchId }).IsUnique().AreNullsDistinct(false).HasFilter("status = 1").HasDatabaseName("ux_carts_active_customer_merchant_branch");
        b.HasIndex(x => x.ExpiresAtUtc); b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field); b.Ignore(x => x.DomainEvents);
    }
}
internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.ToTable("cart_items", CartsPersistence.Schema, t => t.HasCheckConstraint("ck_cart_items_quantity", "quantity > 0 AND quantity <= 99"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new CartItemId(x)); b.Property(x => x.CartId).StrongId(x => x.Value, x => new CartId(x));
        b.Property(x => x.CustomerNote).HasMaxLength(CartRules.MaximumNoteLength); b.HasIndex(x => x.CartId);
        b.HasMany(x => x.SelectedOptions).WithOne().HasForeignKey("CartItemId").OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.SelectedOptions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class CartItemOptionConfiguration : IEntityTypeConfiguration<CartItemOption>
{
    public void Configure(EntityTypeBuilder<CartItemOption> b)
    {
        b.ToTable("cart_item_options", CartsPersistence.Schema, t => t.HasCheckConstraint("ck_cart_item_options_quantity", "quantity > 0"));
        b.Property<CartItemId>("CartItemId").StrongId(x => x.Value, x => new CartItemId(x));
        b.HasKey("CartItemId", nameof(CartItemOption.OptionGroupId), nameof(CartItemOption.OptionItemId));
    }
}
internal sealed class CartIdempotencyConfiguration : IEntityTypeConfiguration<CartIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<CartIdempotencyRecord> b)
    {
        b.ToTable("cart_idempotency_records", CartsPersistence.Schema); b.HasKey(x => x.Id); b.Property(x => x.Id).StrongId(x => x.Value, x => new CartIdempotencyRecordId(x));
        b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.KeyHash).HasMaxLength(64); b.Property(x => x.RequestHash).HasMaxLength(64);
        b.HasIndex(x => new { x.CustomerId, x.Operation, x.KeyHash }).IsUnique().HasDatabaseName("ux_cart_idempotency_customer_operation_key");
        b.HasIndex(x => x.ExpiresAtUtc);
    }
}

