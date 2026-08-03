using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Orders.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMerchantOrderOperations : Migration
{
    private static readonly string[] BranchStatusSubmittedColumns = ["merchant_branch_id", "status", "submitted_at_utc"];
    private static readonly string[] MerchantStatusSubmittedColumns = ["merchant_id", "status", "submitted_at_utc"];
    private static readonly string[] AuditMerchantOccurredColumns = ["merchant_id", "occurred_at_utc"];
    private static readonly string[] AuditOrderOccurredColumns = ["order_id", "occurred_at_utc"];
    private static readonly string[] OperationIdempotencyKeyColumns = ["actor_id", "operation", "key_hash"];
    private static readonly string[] MerchantStatusCreatedColumns = ["merchant_id", "status", "created_at_utc"];
    private static readonly string[] CreationIdempotencyKeyColumns = ["customer_id", "operation", "key_hash"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_order_creation_idempotency_orders_order_id",
            schema: "orders",
            table: "order_creation_idempotency");

        migrationBuilder.DropPrimaryKey(
            name: "pk_order_creation_idempotency",
            schema: "orders",
            table: "order_creation_idempotency");

        migrationBuilder.DropCheckConstraint(
            name: "ck_order_creation_idempotency_hashes",
            schema: "orders",
            table: "order_creation_idempotency");

        migrationBuilder.DropIndex(
            name: "ux_order_creation_idempotency_customer_operation_key",
            schema: "orders",
            table: "order_creation_idempotency");

        migrationBuilder.DropIndex(
            name: "ux_order_creation_idempotency_order_id",
            schema: "orders",
            table: "order_creation_idempotency");

        migrationBuilder.RenameTable(
            name: "order_creation_idempotency",
            schema: "orders",
            newName: "order_operation_idempotency",
            newSchema: "orders");

        migrationBuilder.RenameColumn(
            name: "customer_id",
            schema: "orders",
            table: "order_operation_idempotency",
            newName: "actor_id");

        migrationBuilder.AddPrimaryKey(
            name: "pk_order_operation_idempotency",
            schema: "orders",
            table: "order_operation_idempotency",
            column: "id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_order_operation_idempotency_hashes",
            schema: "orders",
            table: "order_operation_idempotency",
            sql: "char_length(key_hash) = 64 AND char_length(request_hash) = 64");

        migrationBuilder.AddForeignKey(
            name: "fk_order_operation_idempotency_orders_order_id",
            schema: "orders",
            table: "order_operation_idempotency",
            column: "order_id",
            principalSchema: "orders",
            principalTable: "orders",
            principalColumn: "id");

        migrationBuilder.DropIndex(
            name: "ix_orders_merchant_status_created",
            schema: "orders",
            table: "orders");

        migrationBuilder.AddColumn<int>(
            name: "estimated_preparation_minutes",
            schema: "orders",
            table: "orders",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "estimated_ready_at_utc",
            schema: "orders",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "merchant_accepted_by_user_id",
            schema: "orders",
            table: "orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "merchant_rejected_by_user_id",
            schema: "orders",
            table: "orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "merchant_rejection_note",
            schema: "orders",
            table: "orders",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<short>(
            name: "merchant_rejection_reason",
            schema: "orders",
            table: "orders",
            type: "smallint",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "merchant_order_audit",
            schema: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                table.PrimaryKey("pk_merchant_order_audit", x => x.id);
                table.CheckConstraint("ck_merchant_order_audit_idempotency_hash", "char_length(idempotency_key_hash) = 64");
                table.CheckConstraint("ck_merchant_order_audit_operation", "char_length(operation) > 0");
                table.ForeignKey(
                    name: "fk_merchant_order_audit_orders_order_id",
                    column: x => x.order_id,
                    principalSchema: "orders",
                    principalTable: "orders",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_orders_branch_status_submitted",
            schema: "orders",
            table: "orders",
            columns: BranchStatusSubmittedColumns);

        migrationBuilder.CreateIndex(
            name: "ix_orders_merchant_status_submitted",
            schema: "orders",
            table: "orders",
            columns: MerchantStatusSubmittedColumns);

        migrationBuilder.CreateIndex(
            name: "ix_orders_updated_at_utc",
            schema: "orders",
            table: "orders",
            column: "updated_at_utc");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_estimated_ready",
            schema: "orders",
            table: "orders",
            sql: "estimated_ready_at_utc IS NULL OR accepted_at_utc IS NOT NULL AND estimated_ready_at_utc >= accepted_at_utc");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_merchant_rejection_reason",
            schema: "orders",
            table: "orders",
            sql: "merchant_rejection_reason IS NULL OR merchant_rejection_reason BETWEEN 1 AND 6");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_preparation_minutes",
            schema: "orders",
            table: "orders",
            sql: "estimated_preparation_minutes IS NULL OR estimated_preparation_minutes BETWEEN 1 AND 240");

        migrationBuilder.CreateIndex(
            name: "ix_merchant_order_audit_merchant_occurred",
            schema: "orders",
            table: "merchant_order_audit",
            columns: AuditMerchantOccurredColumns);

        migrationBuilder.CreateIndex(
            name: "ix_merchant_order_audit_order_occurred",
            schema: "orders",
            table: "merchant_order_audit",
            columns: AuditOrderOccurredColumns);

        migrationBuilder.CreateIndex(
            name: "ix_order_operation_idempotency_order_id",
            schema: "orders",
            table: "order_operation_idempotency",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ux_order_operation_idempotency_actor_operation_key",
            schema: "orders",
            table: "order_operation_idempotency",
            columns: OperationIdempotencyKeyColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "merchant_order_audit",
            schema: "orders");

        migrationBuilder.DropForeignKey(
            name: "fk_order_operation_idempotency_orders_order_id",
            schema: "orders",
            table: "order_operation_idempotency");

        migrationBuilder.DropPrimaryKey(
            name: "pk_order_operation_idempotency",
            schema: "orders",
            table: "order_operation_idempotency");

        migrationBuilder.DropCheckConstraint(
            name: "ck_order_operation_idempotency_hashes",
            schema: "orders",
            table: "order_operation_idempotency");

        migrationBuilder.DropIndex(
            name: "ix_order_operation_idempotency_order_id",
            schema: "orders",
            table: "order_operation_idempotency");

        migrationBuilder.DropIndex(
            name: "ux_order_operation_idempotency_actor_operation_key",
            schema: "orders",
            table: "order_operation_idempotency");

        migrationBuilder.RenameColumn(
            name: "actor_id",
            schema: "orders",
            table: "order_operation_idempotency",
            newName: "customer_id");

        migrationBuilder.RenameTable(
            name: "order_operation_idempotency",
            schema: "orders",
            newName: "order_creation_idempotency",
            newSchema: "orders");

        migrationBuilder.AddPrimaryKey(
            name: "pk_order_creation_idempotency",
            schema: "orders",
            table: "order_creation_idempotency",
            column: "id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_order_creation_idempotency_hashes",
            schema: "orders",
            table: "order_creation_idempotency",
            sql: "char_length(key_hash) = 64 AND char_length(request_hash) = 64");

        migrationBuilder.AddForeignKey(
            name: "fk_order_creation_idempotency_orders_order_id",
            schema: "orders",
            table: "order_creation_idempotency",
            column: "order_id",
            principalSchema: "orders",
            principalTable: "orders",
            principalColumn: "id");

        migrationBuilder.DropIndex(
            name: "ix_orders_branch_status_submitted",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropIndex(
            name: "ix_orders_merchant_status_submitted",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropIndex(
            name: "ix_orders_updated_at_utc",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_estimated_ready",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_merchant_rejection_reason",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_preparation_minutes",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "estimated_preparation_minutes",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "estimated_ready_at_utc",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "merchant_accepted_by_user_id",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "merchant_rejected_by_user_id",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "merchant_rejection_note",
            schema: "orders",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "merchant_rejection_reason",
            schema: "orders",
            table: "orders");

        migrationBuilder.CreateIndex(
            name: "ix_orders_merchant_status_created",
            schema: "orders",
            table: "orders",
            columns: MerchantStatusCreatedColumns);

        migrationBuilder.CreateIndex(
            name: "ux_order_creation_idempotency_customer_operation_key",
            schema: "orders",
            table: "order_creation_idempotency",
            columns: CreationIdempotencyKeyColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_order_creation_idempotency_order_id",
            schema: "orders",
            table: "order_creation_idempotency",
            column: "order_id",
            unique: true);
    }
}
