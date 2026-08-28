using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace AlSsareea.Modules.Delivery.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitializeDeliveryModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "delivery");

        migrationBuilder.CreateTable(
            name: "deliveries",
            schema: "delivery",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<short>(type: "smallint", nullable: false),
                pickup_merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                pickup_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                pickup_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                pickup_contact_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                pickup_phone_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                pickup_instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                pickup_latitude = table.Column<double>(type: "double precision", nullable: true),
                pickup_longitude = table.Column<double>(type: "double precision", nullable: true),
                drop_off_address_id = table.Column<Guid>(type: "uuid", nullable: false),
                drop_off_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                drop_off_recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                drop_off_phone_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                drop_off_floor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                drop_off_instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                drop_off_latitude = table.Column<double>(type: "double precision", nullable: true),
                drop_off_longitude = table.Column<double>(type: "double precision", nullable: true),
                proof_requirements = table.Column<short>(type: "smallint", nullable: false),
                pin_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                pin_salt = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                pin_failed_attempts = table.Column<int>(type: "integer", nullable: false),
                pin_locked = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                heading_to_pickup_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                arrived_at_pickup_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                picked_up_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                arrived_at_drop_off_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                delivered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                failed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                failure_reason = table.Column<short>(type: "smallint", nullable: true),
                failure_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_deliveries", x => x.id);
                table.CheckConstraint("ck_deliveries_driver_assignment", "driver_id IS NULL OR assigned_at_utc IS NOT NULL");
                table.CheckConstraint("ck_deliveries_pin_attempts", "pin_failed_attempts BETWEEN 0 AND 5");
                table.CheckConstraint("ck_deliveries_pin_configuration", "(proof_requirements & 1) = 0 OR pin_hash IS NOT NULL AND pin_salt IS NOT NULL");
                table.CheckConstraint("ck_deliveries_proof_requirements", "proof_requirements BETWEEN 0 AND 15");
                table.CheckConstraint("ck_deliveries_status", "status BETWEEN 1 AND 10");
                table.CheckConstraint("ck_deliveries_terminal_timestamps", "(status <> 8 OR delivered_at_utc IS NOT NULL) AND (status <> 9 OR failed_at_utc IS NOT NULL) AND (status <> 10 OR cancelled_at_utc IS NOT NULL)");
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "delivery",
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
                table.PrimaryKey("pk_delivery_outbox_messages", x => x.id);
                table.CheckConstraint("ck_delivery_outbox_attempts", "attempt_count >= 0");
                table.CheckConstraint("ck_delivery_outbox_payload", "jsonb_typeof(payload) = 'object'");
            });

        migrationBuilder.CreateTable(
            name: "delivery_audit",
            schema: "delivery",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                old_status = table.Column<short>(type: "smallint", nullable: false),
                new_status = table.Column<short>(type: "smallint", nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                idempotency_key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                safe_reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_delivery_audit", x => x.id);
                table.CheckConstraint("ck_delivery_audit_key_hash", "char_length(idempotency_key_hash) = 64");
                table.ForeignKey(
                    name: "fk_delivery_audit_deliveries_delivery_id",
                    column: x => x.delivery_id,
                    principalSchema: "delivery",
                    principalTable: "deliveries",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "delivery_operation_idempotency",
            schema: "delivery",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_delivery_operation_idempotency", x => x.id);
                table.CheckConstraint("ck_delivery_idempotency_hashes", "char_length(key_hash) = 64 AND char_length(request_hash) = 64");
                table.ForeignKey(
                    name: "fk_delivery_idempotency_deliveries_delivery_id",
                    column: x => x.delivery_id,
                    principalSchema: "delivery",
                    principalTable: "deliveries",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "delivery_proofs",
            schema: "delivery",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<short>(type: "smallint", nullable: false),
                media_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_delivery_proofs", x => x.id);
                table.CheckConstraint("ck_delivery_proofs_content", "(type IN (2,3) AND media_asset_id IS NOT NULL AND recipient_name IS NULL) OR (type = 4 AND media_asset_id IS NULL AND recipient_name IS NOT NULL) OR (type = 1 AND media_asset_id IS NULL AND recipient_name IS NULL)");
                table.ForeignKey(
                    name: "fk_delivery_proofs_deliveries_delivery_id",
                    column: x => x.delivery_id,
                    principalSchema: "delivery",
                    principalTable: "deliveries",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "delivery_status_history",
            schema: "delivery",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                previous_status = table.Column<short>(type: "smallint", nullable: true),
                new_status = table.Column<short>(type: "smallint", nullable: false),
                source = table.Column<short>(type: "smallint", nullable: false),
                changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                reason_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_delivery_status_history", x => x.id);
                table.CheckConstraint("ck_delivery_status_history_changed", "previous_status IS NULL OR previous_status <> new_status");
                table.ForeignKey(
                    name: "fk_delivery_status_history_deliveries_delivery_id",
                    column: x => x.delivery_id,
                    principalSchema: "delivery",
                    principalTable: "deliveries",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_deliveries_customer_id",
            schema: "delivery",
            table: "deliveries",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "ix_deliveries_customer_status",
            schema: "delivery",
            table: "deliveries",
            columns: new[] { "customer_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_deliveries_customer_user_id",
            schema: "delivery",
            table: "deliveries",
            column: "customer_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_deliveries_driver_id",
            schema: "delivery",
            table: "deliveries",
            column: "driver_id");

        migrationBuilder.CreateIndex(
            name: "ix_deliveries_driver_status",
            schema: "delivery",
            table: "deliveries",
            columns: new[] { "driver_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_deliveries_merchant_id",
            schema: "delivery",
            table: "deliveries",
            column: "merchant_id");

        migrationBuilder.CreateIndex(
            name: "ix_deliveries_status",
            schema: "delivery",
            table: "deliveries",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ux_deliveries_order_id",
            schema: "delivery",
            table: "deliveries",
            column: "order_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_delivery_audit_delivery_occurred",
            schema: "delivery",
            table: "delivery_audit",
            columns: new[] { "delivery_id", "occurred_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ix_delivery_idempotency_delivery_id",
            schema: "delivery",
            table: "delivery_operation_idempotency",
            column: "delivery_id");

        migrationBuilder.CreateIndex(
            name: "ux_delivery_idempotency_actor_operation_key",
            schema: "delivery",
            table: "delivery_operation_idempotency",
            columns: new[] { "actor_id", "operation", "key_hash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_delivery_proofs_media_asset_id",
            schema: "delivery",
            table: "delivery_proofs",
            column: "media_asset_id");

        migrationBuilder.CreateIndex(
            name: "ux_delivery_proofs_delivery_type",
            schema: "delivery",
            table: "delivery_proofs",
            columns: new[] { "delivery_id", "type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_delivery_status_history_delivery_changed",
            schema: "delivery",
            table: "delivery_status_history",
            columns: new[] { "delivery_id", "changed_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ix_delivery_outbox_pending",
            schema: "delivery",
            table: "outbox_messages",
            columns: new[] { "processed_at_utc", "occurred_at_utc" },
            filter: "processed_at_utc IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "delivery_audit",
            schema: "delivery");

        migrationBuilder.DropTable(
            name: "delivery_operation_idempotency",
            schema: "delivery");

        migrationBuilder.DropTable(
            name: "delivery_proofs",
            schema: "delivery");

        migrationBuilder.DropTable(
            name: "delivery_status_history",
            schema: "delivery");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "delivery");

        migrationBuilder.DropTable(
            name: "deliveries",
            schema: "delivery");
    }
}
