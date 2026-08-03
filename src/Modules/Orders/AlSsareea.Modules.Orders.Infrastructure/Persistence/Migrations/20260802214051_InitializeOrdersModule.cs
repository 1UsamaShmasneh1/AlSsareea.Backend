using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Orders.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitializeOrdersModule : Migration
{
    private static readonly string[] IdempotencyKeyColumns = ["customer_id", "operation", "key_hash"];
    private static readonly string[] HistoryOrderColumns = ["order_id", "changed_at_utc"];
    private static readonly string[] CustomerOrderColumns = ["customer_id", "created_at_utc"];
    private static readonly string[] MerchantOrderColumns = ["merchant_id", "status", "created_at_utc"];
    private static readonly string[] PendingOutboxColumns = ["processed_at_utc", "occurred_at_utc"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "orders");

        migrationBuilder.CreateTable(
            name: "orders",
            schema: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                source_cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<short>(type: "smallint", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                subtotal_minor = table.Column<long>(type: "bigint", nullable: false),
                options_total_minor = table.Column<long>(type: "bigint", nullable: false),
                product_discount_minor = table.Column<long>(type: "bigint", nullable: false),
                coupon_discount_minor = table.Column<long>(type: "bigint", nullable: false),
                delivery_discount_minor = table.Column<long>(type: "bigint", nullable: false),
                delivery_fee_minor = table.Column<long>(type: "bigint", nullable: false),
                service_fee_minor = table.Column<long>(type: "bigint", nullable: false),
                platform_fee_minor = table.Column<long>(type: "bigint", nullable: false),
                small_order_fee_minor = table.Column<long>(type: "bigint", nullable: false),
                tax_minor = table.Column<long>(type: "bigint", nullable: false),
                total_minor = table.Column<long>(type: "bigint", nullable: false),
                pricing_reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                pricing_calculated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                snapshot_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                customer_phone_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                customer_preferred_language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                address_id = table.Column<Guid>(type: "uuid", nullable: false),
                address_label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                address_city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                address_area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                address_street = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                address_building_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                address_floor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                address_apartment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                address_delivery_instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                address_latitude = table.Column<double>(type: "double precision", nullable: true),
                address_longitude = table.Column<double>(type: "double precision", nullable: true),
                address_place_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                address_formatted = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                snapshot_merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                snapshot_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                merchant_display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                branch_display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                branch_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                branch_phone_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                scheduled_for_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                customer_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                merchant_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                cancellation_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                cancelled_by = table.Column<short>(type: "smallint", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                accepted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                rejected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                preparing_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ready_for_pickup_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                driver_assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                picked_up_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                delivered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                failed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_orders", x => x.id);
                table.CheckConstraint("ck_orders_currency", "char_length(currency) = 3");
                table.CheckConstraint("ck_orders_money_non_negative", "subtotal_minor >= 0 AND options_total_minor >= 0 AND product_discount_minor >= 0 AND coupon_discount_minor >= 0 AND delivery_discount_minor >= 0 AND delivery_fee_minor >= 0 AND service_fee_minor >= 0 AND platform_fee_minor >= 0 AND small_order_fee_minor >= 0 AND tax_minor >= 0 AND total_minor >= 0");
                table.CheckConstraint("ck_orders_scheduled", "scheduled_for_utc IS NULL OR scheduled_for_utc > created_at_utc");
                table.CheckConstraint("ck_orders_total", "total_minor = subtotal_minor + delivery_fee_minor + service_fee_minor + platform_fee_minor + small_order_fee_minor + tax_minor - product_discount_minor - coupon_discount_minor - delivery_discount_minor");
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                error_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_outbox_messages", x => x.id);
                table.CheckConstraint("ck_order_outbox_attempts", "attempt_count >= 0");
                table.CheckConstraint("ck_order_outbox_event_type", "char_length(event_type) > 0");
                table.CheckConstraint("ck_order_outbox_payload", "jsonb_typeof(payload) = 'object'");
            });

        migrationBuilder.CreateTable(
            name: "order_creation_idempotency",
            schema: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_creation_idempotency", x => x.id);
                table.CheckConstraint("ck_order_creation_idempotency_hashes", "char_length(key_hash) = 64 AND char_length(request_hash) = 64");
                table.ForeignKey(
                    name: "fk_order_creation_idempotency_orders_order_id",
                    column: x => x.order_id,
                    principalSchema: "orders",
                    principalTable: "orders",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "order_items",
            schema: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_version = table.Column<int>(type: "integer", nullable: false),
                variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                product_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                variant_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                sku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                quantity = table.Column<int>(type: "integer", nullable: false),
                unit_base_price_minor = table.Column<long>(type: "bigint", nullable: false),
                unit_options_price_minor = table.Column<long>(type: "bigint", nullable: false),
                unit_discount_minor = table.Column<long>(type: "bigint", nullable: false),
                unit_final_price_minor = table.Column<long>(type: "bigint", nullable: false),
                line_subtotal_minor = table.Column<long>(type: "bigint", nullable: false),
                line_discount_minor = table.Column<long>(type: "bigint", nullable: false),
                line_total_minor = table.Column<long>(type: "bigint", nullable: false),
                customer_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_items", x => x.id);
                table.CheckConstraint("ck_order_items_money", "unit_base_price_minor >= 0 AND unit_options_price_minor >= 0 AND unit_discount_minor >= 0 AND unit_final_price_minor >= 0 AND line_subtotal_minor >= 0 AND line_discount_minor >= 0 AND line_total_minor >= 0");
                table.CheckConstraint("ck_order_items_quantity", "quantity > 0");
                table.ForeignKey(
                    name: "fk_order_items_orders_order_id",
                    column: x => x.order_id,
                    principalSchema: "orders",
                    principalTable: "orders",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "order_status_history",
            schema: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                previous_status = table.Column<short>(type: "smallint", nullable: true),
                new_status = table.Column<short>(type: "smallint", nullable: false),
                changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                change_source = table.Column<short>(type: "smallint", nullable: false),
                reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                reason_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_status_history", x => x.id);
                table.CheckConstraint("ck_order_status_history_changed", "previous_status IS NULL OR previous_status <> new_status");
                table.ForeignKey(
                    name: "fk_order_status_history_orders_order_id",
                    column: x => x.order_id,
                    principalSchema: "orders",
                    principalTable: "orders",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "order_item_options",
            schema: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                option_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                option_id = table.Column<Guid>(type: "uuid", nullable: false),
                option_group_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                option_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                unit_price_adjustment_minor = table.Column<long>(type: "bigint", nullable: false),
                total_price_adjustment_minor = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_item_options", x => x.id);
                table.CheckConstraint("ck_order_item_options_quantity", "quantity > 0");
                table.ForeignKey(
                    name: "fk_order_item_options_order_items_order_item_id",
                    column: x => x.order_item_id,
                    principalSchema: "orders",
                    principalTable: "order_items",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ux_order_creation_idempotency_customer_operation_key",
            schema: "orders",
            table: "order_creation_idempotency",
            columns: IdempotencyKeyColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_order_creation_idempotency_order_id",
            schema: "orders",
            table: "order_creation_idempotency",
            column: "order_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_order_item_options_order_item_id",
            schema: "orders",
            table: "order_item_options",
            column: "order_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_items_order_id",
            schema: "orders",
            table: "order_items",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_items_product_id",
            schema: "orders",
            table: "order_items",
            column: "product_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_status_history_order_changed",
            schema: "orders",
            table: "order_status_history",
            columns: HistoryOrderColumns);

        migrationBuilder.CreateIndex(
            name: "ix_order_status_history_order_id",
            schema: "orders",
            table: "order_status_history",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ix_orders_created_at_utc",
            schema: "orders",
            table: "orders",
            column: "created_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_orders_customer_created",
            schema: "orders",
            table: "orders",
            columns: CustomerOrderColumns);

        migrationBuilder.CreateIndex(
            name: "ix_orders_customer_id",
            schema: "orders",
            table: "orders",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "ix_orders_merchant_branch_id",
            schema: "orders",
            table: "orders",
            column: "merchant_branch_id");

        migrationBuilder.CreateIndex(
            name: "ix_orders_merchant_id",
            schema: "orders",
            table: "orders",
            column: "merchant_id");

        migrationBuilder.CreateIndex(
            name: "ix_orders_merchant_status_created",
            schema: "orders",
            table: "orders",
            columns: MerchantOrderColumns);

        migrationBuilder.CreateIndex(
            name: "ix_orders_scheduled_for_utc",
            schema: "orders",
            table: "orders",
            column: "scheduled_for_utc");

        migrationBuilder.CreateIndex(
            name: "ix_orders_status",
            schema: "orders",
            table: "orders",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ux_orders_order_number",
            schema: "orders",
            table: "orders",
            column: "order_number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_orders_source_cart_id",
            schema: "orders",
            table: "orders",
            column: "source_cart_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_order_outbox_occurred_at_utc",
            schema: "orders",
            table: "outbox_messages",
            column: "occurred_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_order_outbox_pending",
            schema: "orders",
            table: "outbox_messages",
            columns: PendingOutboxColumns,
            filter: "processed_at_utc IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_order_outbox_processed_at_utc",
            schema: "orders",
            table: "outbox_messages",
            column: "processed_at_utc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_creation_idempotency",
            schema: "orders");

        migrationBuilder.DropTable(
            name: "order_item_options",
            schema: "orders");

        migrationBuilder.DropTable(
            name: "order_status_history",
            schema: "orders");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "orders");

        migrationBuilder.DropTable(
            name: "order_items",
            schema: "orders");

        migrationBuilder.DropTable(
            name: "orders",
            schema: "orders");
    }
}
