using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration index column arrays are immutable.

namespace AlSsareea.Modules.Promotions.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPromotionsModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "promotions");

        migrationBuilder.CreateTable(
            name: "promotions",
            schema: "promotions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                internal_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                display_name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                display_name_he = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                display_name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                description_he = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                description_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                type = table.Column<short>(type: "smallint", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                stackability = table.Column<short>(type: "smallint", nullable: false),
                conflict_group = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                funding_source = table.Column<short>(type: "smallint", nullable: false),
                platform_share_basis_points = table.Column<int>(type: "integer", nullable: false),
                merchant_share_basis_points = table.Column<int>(type: "integer", nullable: false),
                starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                global_usage_limit = table.Column<long>(type: "bigint", nullable: true),
                per_customer_usage_limit = table.Column<long>(type: "bigint", nullable: true),
                budget_limit_minor = table.Column<long>(type: "bigint", nullable: true),
                maximum_redemptions_per_order = table.Column<int>(type: "integer", nullable: true),
                minimum_subtotal_minor = table.Column<long>(type: "bigint", nullable: true),
                eligible_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                first_order_only = table.Column<bool>(type: "boolean", nullable: false),
                scope_type = table.Column<short>(type: "smallint", nullable: false),
                scope_target_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                scope_merchant_id = table.Column<Guid>(type: "uuid", nullable: true),
                benefit_kind = table.Column<short>(type: "smallint", nullable: false),
                currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                benefit_value = table.Column<long>(type: "bigint", nullable: false),
                maximum_discount_minor = table.Column<long>(type: "bigint", nullable: true),
                normalized_coupon_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                suspended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                archived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_promotions", x => x.id);
                table.CheckConstraint("ck_promotions_benefit", "benefit_value >= 0 AND (maximum_discount_minor IS NULL OR maximum_discount_minor >= 0)");
                table.CheckConstraint("ck_promotions_funding", "platform_share_basis_points >= 0 AND merchant_share_basis_points >= 0 AND platform_share_basis_points + merchant_share_basis_points = 10000");
                table.CheckConstraint("ck_promotions_priority", "priority BETWEEN -100000 AND 100000");
                table.CheckConstraint("ck_promotions_status", "status BETWEEN 1 AND 5");
                table.CheckConstraint("ck_promotions_type", "type BETWEEN 1 AND 6");
                table.CheckConstraint("ck_promotions_usage_limits", "(global_usage_limit IS NULL OR global_usage_limit > 0) AND (per_customer_usage_limit IS NULL OR per_customer_usage_limit > 0) AND (budget_limit_minor IS NULL OR budget_limit_minor > 0)");
                table.CheckConstraint("ck_promotions_validity", "ends_at_utc > starts_at_utc");
            });

        migrationBuilder.CreateTable(
            name: "promotion_audit",
            schema: "promotions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                promotion_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_promotion_audit", x => x.id);
                table.ForeignKey(
                    name: "fk_promotion_audit_promotions_promotion_id",
                    column: x => x.promotion_id,
                    principalSchema: "promotions",
                    principalTable: "promotions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "promotion_redemptions",
            schema: "promotions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                promotion_id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                external_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                discount_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_promotion_redemptions", x => x.id);
                table.CheckConstraint("ck_promotion_redemptions_amount", "discount_amount_minor >= 0");
                table.ForeignKey(
                    name: "fk_promotion_redemptions_promotions_promotion_id",
                    column: x => x.promotion_id,
                    principalSchema: "promotions",
                    principalTable: "promotions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_promotion_audit_promotion_occurred",
            schema: "promotions",
            table: "promotion_audit",
            columns: new[] { "promotion_id", "occurred_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ix_promotion_redemptions_customer_id",
            schema: "promotions",
            table: "promotion_redemptions",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "ix_promotion_redemptions_promotion_id",
            schema: "promotions",
            table: "promotion_redemptions",
            column: "promotion_id");

        migrationBuilder.CreateIndex(
            name: "ux_promotion_redemptions_external_reference",
            schema: "promotions",
            table: "promotion_redemptions",
            column: "external_reference",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_promotions_active_priority",
            schema: "promotions",
            table: "promotions",
            columns: new[] { "status", "priority" },
            filter: "status = 2");

        migrationBuilder.CreateIndex(
            name: "ix_promotions_priority",
            schema: "promotions",
            table: "promotions",
            column: "priority");

        migrationBuilder.CreateIndex(
            name: "ix_promotions_scope_merchant_id",
            schema: "promotions",
            table: "promotions",
            column: "scope_merchant_id");

        migrationBuilder.CreateIndex(
            name: "ix_promotions_scope_target_ids",
            schema: "promotions",
            table: "promotions",
            column: "scope_target_ids")
            .Annotation("Npgsql:IndexMethod", "gin");

        migrationBuilder.CreateIndex(
            name: "ix_promotions_status",
            schema: "promotions",
            table: "promotions",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_promotions_type",
            schema: "promotions",
            table: "promotions",
            column: "type");

        migrationBuilder.CreateIndex(
            name: "ix_promotions_validity",
            schema: "promotions",
            table: "promotions",
            columns: new[] { "starts_at_utc", "ends_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ux_promotions_internal_name",
            schema: "promotions",
            table: "promotions",
            column: "internal_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_promotions_normalized_coupon_code",
            schema: "promotions",
            table: "promotions",
            column: "normalized_coupon_code",
            unique: true,
            filter: "normalized_coupon_code IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "promotion_audit",
            schema: "promotions");

        migrationBuilder.DropTable(
            name: "promotion_redemptions",
            schema: "promotions");

        migrationBuilder.DropTable(
            name: "promotions",
            schema: "promotions");
    }
}
#pragma warning restore CA1861
