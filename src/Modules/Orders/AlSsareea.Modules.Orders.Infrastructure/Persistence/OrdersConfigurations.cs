using AlSsareea.Modules.Orders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Orders.Infrastructure.Persistence;

internal static class OrderConfigurationExtensions
{
    internal static PropertyBuilder<T> StrongId<T>(this PropertyBuilder<T> property, Func<T, Guid> toGuid, Func<Guid, T> fromGuid) => property.HasConversion(value => toGuid(value), value => fromGuid(value)).HasColumnType("uuid").ValueGeneratedNever();
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders", OrdersPersistence.Schema, t =>
        {
            t.HasCheckConstraint("ck_orders_currency", "char_length(currency) = 3");
            t.HasCheckConstraint("ck_orders_money_non_negative", "subtotal_minor >= 0 AND options_total_minor >= 0 AND product_discount_minor >= 0 AND coupon_discount_minor >= 0 AND delivery_discount_minor >= 0 AND delivery_fee_minor >= 0 AND service_fee_minor >= 0 AND platform_fee_minor >= 0 AND small_order_fee_minor >= 0 AND tax_minor >= 0 AND total_minor >= 0");
            t.HasCheckConstraint("ck_orders_total", "total_minor = subtotal_minor + delivery_fee_minor + service_fee_minor + platform_fee_minor + small_order_fee_minor + tax_minor - product_discount_minor - coupon_discount_minor - delivery_discount_minor");
            t.HasCheckConstraint("ck_orders_scheduled", "scheduled_for_utc IS NULL OR scheduled_for_utc > created_at_utc");
            t.HasCheckConstraint("ck_orders_preparation_minutes", "estimated_preparation_minutes IS NULL OR estimated_preparation_minutes BETWEEN 1 AND 240");
            t.HasCheckConstraint("ck_orders_estimated_ready", "estimated_ready_at_utc IS NULL OR accepted_at_utc IS NOT NULL AND estimated_ready_at_utc >= accepted_at_utc");
            t.HasCheckConstraint("ck_orders_merchant_rejection_reason", "merchant_rejection_reason IS NULL OR merchant_rejection_reason BETWEEN 1 AND 6");
        });
        b.HasKey(x => x.Id).HasName("pk_orders"); b.Property(x => x.Id).StrongId(x => x.Value, x => new OrderId(x));
        b.Property(x => x.OrderNumber).HasMaxLength(OrderRules.OrderNumberMaximumLength); b.Property(x => x.Type).HasConversion<short>(); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.Currency).HasMaxLength(OrderRules.CurrencyLength).IsFixedLength();
        b.Property(x => x.CustomerNotes).HasMaxLength(OrderRules.CustomerNotesMaximumLength); b.Property(x => x.MerchantNotes).HasMaxLength(OrderRules.MerchantNotesMaximumLength); b.Property(x => x.CancellationCode).HasMaxLength(OrderRules.ReasonCodeMaximumLength); b.Property(x => x.CancellationReason).HasMaxLength(OrderRules.ReasonTextMaximumLength); b.Property(x => x.CancelledBy).HasConversion<short>();
        b.Property(x => x.MerchantRejectionReason).HasConversion<short>(); b.Property(x => x.MerchantRejectionNote).HasMaxLength(OrderRules.ReasonTextMaximumLength);
        b.Property(x => x.PricingReference).HasMaxLength(160); b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        b.HasIndex(x => x.OrderNumber).IsUnique().HasDatabaseName("ux_orders_order_number"); b.HasIndex(x => x.SourceCartId).IsUnique().HasDatabaseName("ux_orders_source_cart_id"); b.HasIndex(x => x.CustomerId).HasDatabaseName("ix_orders_customer_id"); b.HasIndex(x => x.MerchantId).HasDatabaseName("ix_orders_merchant_id"); b.HasIndex(x => x.MerchantBranchId).HasDatabaseName("ix_orders_merchant_branch_id"); b.HasIndex(x => x.Status).HasDatabaseName("ix_orders_status"); b.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("ix_orders_created_at_utc"); b.HasIndex(x => x.UpdatedAtUtc).HasDatabaseName("ix_orders_updated_at_utc"); b.HasIndex(x => x.ScheduledForUtc).HasDatabaseName("ix_orders_scheduled_for_utc"); b.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc }).HasDatabaseName("ix_orders_customer_created"); b.HasIndex(x => new { x.MerchantId, x.Status, x.SubmittedAtUtc }).HasDatabaseName("ix_orders_merchant_status_submitted"); b.HasIndex(x => new { x.MerchantBranchId, x.Status, x.SubmittedAtUtc }).HasDatabaseName("ix_orders_branch_status_submitted");
        b.OwnsOne(x => x.Customer, owned =>
        {
            owned.Property(x => x.CustomerId).HasColumnName("snapshot_customer_id"); owned.Property(x => x.DisplayName).HasColumnName("customer_display_name").HasMaxLength(OrderRules.NameMaximumLength); owned.Property(x => x.PhoneNumber).HasColumnName("customer_phone_number").HasMaxLength(40); owned.Property(x => x.PreferredLanguage).HasColumnName("customer_preferred_language").HasMaxLength(5);
        });
        b.OwnsOne(x => x.DeliveryAddress, owned =>
        {
            owned.Property(x => x.AddressId).HasColumnName("address_id"); owned.Property(x => x.Label).HasColumnName("address_label").HasMaxLength(80); owned.Property(x => x.City).HasColumnName("address_city").HasMaxLength(120); owned.Property(x => x.Area).HasColumnName("address_area").HasMaxLength(120); owned.Property(x => x.Street).HasColumnName("address_street").HasMaxLength(OrderRules.AddressMaximumLength); owned.Property(x => x.BuildingNumber).HasColumnName("address_building_number").HasMaxLength(40); owned.Property(x => x.Floor).HasColumnName("address_floor").HasMaxLength(40); owned.Property(x => x.Apartment).HasColumnName("address_apartment").HasMaxLength(40); owned.Property(x => x.DeliveryInstructions).HasColumnName("address_delivery_instructions").HasMaxLength(1000); owned.Property(x => x.Latitude).HasColumnName("address_latitude"); owned.Property(x => x.Longitude).HasColumnName("address_longitude"); owned.Property(x => x.PlaceId).HasColumnName("address_place_id").HasMaxLength(200); owned.Property(x => x.FormattedAddress).HasColumnName("address_formatted").HasMaxLength(500);
        });
        b.OwnsOne(x => x.Merchant, owned =>
        {
            owned.Property(x => x.MerchantId).HasColumnName("snapshot_merchant_id"); owned.Property(x => x.BranchId).HasColumnName("snapshot_branch_id"); owned.Property(x => x.MerchantDisplayName).HasColumnName("merchant_display_name").HasMaxLength(OrderRules.NameMaximumLength); owned.Property(x => x.BranchDisplayName).HasColumnName("branch_display_name").HasMaxLength(OrderRules.NameMaximumLength); owned.Property(x => x.BranchAddress).HasColumnName("branch_address").HasMaxLength(500); owned.Property(x => x.BranchPhoneNumber).HasColumnName("branch_phone_number").HasMaxLength(40);
        });
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_order_items_orders_order_id"); b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.StatusHistory).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_order_status_history_orders_order_id"); b.Navigation(x => x.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field); b.Ignore(x => x.DomainEvents);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("order_items", OrdersPersistence.Schema, t => { t.HasCheckConstraint("ck_order_items_quantity", "quantity > 0"); t.HasCheckConstraint("ck_order_items_money", "unit_base_price_minor >= 0 AND unit_options_price_minor >= 0 AND unit_discount_minor >= 0 AND unit_final_price_minor >= 0 AND line_subtotal_minor >= 0 AND line_discount_minor >= 0 AND line_total_minor >= 0"); });
        b.HasKey(x => x.Id).HasName("pk_order_items"); b.Property(x => x.Id).StrongId(x => x.Value, x => new OrderItemId(x)); b.Property(x => x.OrderId).StrongId(x => x.Value, x => new OrderId(x)); b.Property(x => x.ProductName).HasMaxLength(OrderRules.NameMaximumLength); b.Property(x => x.VariantName).HasMaxLength(OrderRules.NameMaximumLength); b.Property(x => x.Sku).HasMaxLength(120); b.Property(x => x.CustomerNote).HasMaxLength(OrderRules.CustomerNotesMaximumLength);
        b.HasIndex(x => x.OrderId).HasDatabaseName("ix_order_items_order_id"); b.HasIndex(x => x.ProductId).HasDatabaseName("ix_order_items_product_id"); b.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_order_item_options_order_items_order_item_id"); b.Navigation(x => x.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OrderItemOptionConfiguration : IEntityTypeConfiguration<OrderItemOption>
{
    public void Configure(EntityTypeBuilder<OrderItemOption> b)
    {
        b.ToTable("order_item_options", OrdersPersistence.Schema, t => t.HasCheckConstraint("ck_order_item_options_quantity", "quantity > 0")); b.HasKey(x => x.Id).HasName("pk_order_item_options"); b.Property(x => x.Id).StrongId(x => x.Value, x => new OrderItemOptionId(x)); b.Property(x => x.OrderItemId).StrongId(x => x.Value, x => new OrderItemId(x)); b.Property(x => x.OptionGroupName).HasMaxLength(OrderRules.NameMaximumLength); b.Property(x => x.OptionName).HasMaxLength(OrderRules.NameMaximumLength); b.HasIndex(x => x.OrderItemId).HasDatabaseName("ix_order_item_options_order_item_id");
    }
}

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.ToTable("order_status_history", OrdersPersistence.Schema, t => t.HasCheckConstraint("ck_order_status_history_changed", "previous_status IS NULL OR previous_status <> new_status")); b.HasKey(x => x.Id).HasName("pk_order_status_history"); b.Property(x => x.Id).StrongId(x => x.Value, x => new OrderStatusHistoryId(x)); b.Property(x => x.OrderId).StrongId(x => x.Value, x => new OrderId(x)); b.Property(x => x.PreviousStatus).HasConversion<short>(); b.Property(x => x.NewStatus).HasConversion<short>(); b.Property(x => x.ChangeSource).HasConversion<short>(); b.Property(x => x.ReasonCode).HasMaxLength(OrderRules.ReasonCodeMaximumLength); b.Property(x => x.ReasonText).HasMaxLength(OrderRules.ReasonTextMaximumLength); b.Property(x => x.CorrelationId).HasMaxLength(100); b.HasIndex(x => x.OrderId).HasDatabaseName("ix_order_status_history_order_id"); b.HasIndex(x => new { x.OrderId, x.ChangedAtUtc }).HasDatabaseName("ix_order_status_history_order_changed");
    }
}

internal sealed class OrderOperationIdempotencyConfiguration : IEntityTypeConfiguration<OrderOperationIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<OrderOperationIdempotencyRecord> b)
    {
        b.ToTable("order_operation_idempotency", OrdersPersistence.Schema, t => t.HasCheckConstraint("ck_order_operation_idempotency_hashes", "char_length(key_hash) = 64 AND char_length(request_hash) = 64")); b.HasKey(x => x.Id).HasName("pk_order_operation_idempotency"); b.Property(x => x.Id).StrongId(x => x.Value, x => new OrderCreationIdempotencyId(x)); b.Property(x => x.OrderId).StrongId(x => x.Value, x => new OrderId(x)); b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.KeyHash).HasMaxLength(64); b.Property(x => x.RequestHash).HasMaxLength(64); b.HasIndex(x => new { x.ActorId, x.Operation, x.KeyHash }).IsUnique().HasDatabaseName("ux_order_operation_idempotency_actor_operation_key"); b.HasIndex(x => x.OrderId).HasDatabaseName("ix_order_operation_idempotency_order_id"); b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_order_operation_idempotency_orders_order_id");
    }
}

internal sealed class MerchantOrderAuditConfiguration : IEntityTypeConfiguration<MerchantOrderAuditRecord>
{
    public void Configure(EntityTypeBuilder<MerchantOrderAuditRecord> b)
    {
        b.ToTable("merchant_order_audit", OrdersPersistence.Schema, t =>
        {
            t.HasCheckConstraint("ck_merchant_order_audit_operation", "char_length(operation) > 0");
            t.HasCheckConstraint("ck_merchant_order_audit_idempotency_hash", "char_length(idempotency_key_hash) = 64");
        });
        b.HasKey(x => x.Id).HasName("pk_merchant_order_audit");
        b.Property(x => x.Id).StrongId(x => x.Value, x => new MerchantOrderAuditId(x));
        b.Property(x => x.OrderId).StrongId(x => x.Value, x => new OrderId(x));
        b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.OldStatus).HasConversion<short>(); b.Property(x => x.NewStatus).HasConversion<short>();
        b.Property(x => x.CorrelationId).HasMaxLength(100); b.Property(x => x.IdempotencyKeyHash).HasMaxLength(64); b.Property(x => x.SafeReasonCode).HasMaxLength(OrderRules.ReasonCodeMaximumLength);
        b.HasIndex(x => new { x.OrderId, x.OccurredAtUtc }).HasDatabaseName("ix_merchant_order_audit_order_occurred");
        b.HasIndex(x => new { x.MerchantId, x.OccurredAtUtc }).HasDatabaseName("ix_merchant_order_audit_merchant_occurred");
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_merchant_order_audit_orders_order_id");
    }
}

internal sealed class OrderOutboxMessageConfiguration : IEntityTypeConfiguration<OrderOutboxMessage>
{
    public void Configure(EntityTypeBuilder<OrderOutboxMessage> b)
    {
        b.ToTable("outbox_messages", OrdersPersistence.Schema, t => { t.HasCheckConstraint("ck_order_outbox_event_type", "char_length(event_type) > 0"); t.HasCheckConstraint("ck_order_outbox_payload", "jsonb_typeof(payload) = 'object'"); t.HasCheckConstraint("ck_order_outbox_attempts", "attempt_count >= 0"); }); b.HasKey(x => x.Id).HasName("pk_order_outbox_messages"); b.Property(x => x.Id).StrongId(x => x.Value, x => new OrderOutboxMessageId(x)); b.Property(x => x.EventType).HasMaxLength(200); b.Property(x => x.Payload).HasColumnType("jsonb"); b.Property(x => x.ErrorCode).HasMaxLength(200); b.HasIndex(x => x.ProcessedAtUtc).HasDatabaseName("ix_order_outbox_processed_at_utc"); b.HasIndex(x => x.OccurredAtUtc).HasDatabaseName("ix_order_outbox_occurred_at_utc"); b.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc }).HasFilter("processed_at_utc IS NULL").HasDatabaseName("ix_order_outbox_pending");
    }
}
