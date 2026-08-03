using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration index column arrays are immutable.

namespace AlSsareea.Modules.Carts.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCartsModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "carts");

        migrationBuilder.CreateTable(
            name: "cart_idempotency_records",
            schema: "carts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                cart_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_cart_idempotency_records", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "carts",
            schema: "carts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<short>(type: "smallint", nullable: false),
                coupon_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                last_priced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_carts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "cart_items",
            schema: "carts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                quantity = table.Column<int>(type: "integer", nullable: false),
                customer_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                catalog_version = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_cart_items", x => x.id);
                table.CheckConstraint("ck_cart_items_quantity", "quantity > 0 AND quantity <= 99");
                table.ForeignKey(
                    name: "fk_cart_items_carts_cart_id",
                    column: x => x.cart_id,
                    principalSchema: "carts",
                    principalTable: "carts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "cart_item_options",
            schema: "carts",
            columns: table => new
            {
                option_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                option_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                cart_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                catalog_version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_cart_item_options", x => new { x.cart_item_id, x.option_group_id, x.option_item_id });
                table.CheckConstraint("ck_cart_item_options_quantity", "quantity > 0");
                table.ForeignKey(
                    name: "fk_cart_item_options_cart_items_cart_item_id",
                    column: x => x.cart_item_id,
                    principalSchema: "carts",
                    principalTable: "cart_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_cart_idempotency_records_expires_at_utc",
            schema: "carts",
            table: "cart_idempotency_records",
            column: "expires_at_utc");

        migrationBuilder.CreateIndex(
            name: "ux_cart_idempotency_customer_operation_key",
            schema: "carts",
            table: "cart_idempotency_records",
            columns: new[] { "customer_id", "operation", "key_hash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_cart_items_cart_id",
            schema: "carts",
            table: "cart_items",
            column: "cart_id");

        migrationBuilder.CreateIndex(
            name: "ix_carts_expires_at_utc",
            schema: "carts",
            table: "carts",
            column: "expires_at_utc");

        migrationBuilder.CreateIndex(
            name: "ux_carts_active_customer_merchant_branch",
            schema: "carts",
            table: "carts",
            columns: new[] { "customer_id", "merchant_id", "branch_id" },
            unique: true,
            filter: "status = 1")
            .Annotation("Npgsql:NullsDistinct", false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "cart_idempotency_records",
            schema: "carts");

        migrationBuilder.DropTable(
            name: "cart_item_options",
            schema: "carts");

        migrationBuilder.DropTable(
            name: "cart_items",
            schema: "carts");

        migrationBuilder.DropTable(
            name: "carts",
            schema: "carts");
    }
}

