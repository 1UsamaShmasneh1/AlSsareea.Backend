using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Pricing.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialPricing : Migration
{
    private static readonly string[] MerchantBranchColumns = ["merchant_id", "branch_id"];
    private static readonly string[] ScopeLookupColumns = ["scope_key", "currency", "status", "priority", "effective_from_utc"];
    private static readonly string[] EffectiveLookupColumns = ["status", "currency", "effective_from_utc", "effective_until_utc"];
    private static readonly string[] RuleLookupColumns = ["pricing_policy_id", "type", "priority"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "pricing");

        migrationBuilder.CreateTable(
            name: "pricing_policies",
            schema: "pricing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                scope_type = table.Column<short>(type: "smallint", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: true),
                branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                scope_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                effective_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                priority = table.Column<int>(type: "integer", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deactivated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                archived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pricing_policies", x => x.id);
                table.CheckConstraint("ck_pricing_policies_period", "effective_until_utc IS NULL OR effective_until_utc > effective_from_utc");
                table.CheckConstraint("ck_pricing_policies_priority", "priority BETWEEN 0 AND 1000");
                table.CheckConstraint("ck_pricing_policies_scope", "(scope_type = 1 AND merchant_id IS NULL AND branch_id IS NULL AND zone_id IS NULL) OR (scope_type = 2 AND merchant_id IS NULL AND branch_id IS NULL AND zone_id IS NOT NULL) OR (scope_type = 3 AND merchant_id IS NOT NULL AND branch_id IS NULL AND zone_id IS NULL) OR (scope_type = 4 AND merchant_id IS NOT NULL AND branch_id IS NOT NULL AND zone_id IS NULL)");
                table.CheckConstraint("ck_pricing_policies_scope_type", "scope_type BETWEEN 1 AND 4");
                table.CheckConstraint("ck_pricing_policies_status", "status BETWEEN 1 AND 4");
                table.CheckConstraint("ck_pricing_policies_version", "version >= 1");
            });

        migrationBuilder.CreateTable(
            name: "pricing_rules",
            schema: "pricing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<short>(type: "smallint", nullable: false),
                kind = table.Column<short>(type: "smallint", nullable: false),
                calculation_base = table.Column<short>(type: "smallint", nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                amount_minor = table.Column<long>(type: "bigint", nullable: false),
                percentage_basis_points = table.Column<int>(type: "integer", nullable: false),
                threshold_minor = table.Column<long>(type: "bigint", nullable: true),
                minimum_minor = table.Column<long>(type: "bigint", nullable: true),
                maximum_minor = table.Column<long>(type: "bigint", nullable: true),
                included_distance_meters = table.Column<int>(type: "integer", nullable: true),
                maximum_distance_meters = table.Column<int>(type: "integer", nullable: true),
                additional_fee_per_kilometer_minor = table.Column<long>(type: "bigint", nullable: true),
                pricing_policy_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pricing_rules", x => x.id);
                table.CheckConstraint("ck_pricing_rules_amount", "amount_minor >= 0");
                table.CheckConstraint("ck_pricing_rules_base", "calculation_base BETWEEN 1 AND 3");
                table.CheckConstraint("ck_pricing_rules_distance", "(included_distance_meters IS NULL OR included_distance_meters >= 0) AND (maximum_distance_meters IS NULL OR maximum_distance_meters > 0) AND (additional_fee_per_kilometer_minor IS NULL OR additional_fee_per_kilometer_minor >= 0)");
                table.CheckConstraint("ck_pricing_rules_kind", "kind BETWEEN 0 AND 2");
                table.CheckConstraint("ck_pricing_rules_money_limits", "(threshold_minor IS NULL OR threshold_minor >= 0) AND (minimum_minor IS NULL OR minimum_minor >= 0) AND (maximum_minor IS NULL OR maximum_minor >= 0) AND (minimum_minor IS NULL OR maximum_minor IS NULL OR minimum_minor <= maximum_minor)");
                table.CheckConstraint("ck_pricing_rules_percentage", "percentage_basis_points BETWEEN 0 AND 10000");
                table.CheckConstraint("ck_pricing_rules_priority", "priority BETWEEN 0 AND 1000");
                table.CheckConstraint("ck_pricing_rules_type", "type BETWEEN 1 AND 8");
                table.ForeignKey(
                    name: "fk_pricing_rules_pricing_policies_pricing_policy_id",
                    column: x => x.pricing_policy_id,
                    principalSchema: "pricing",
                    principalTable: "pricing_policies",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_pricing_policies_merchant_id_branch_id",
            schema: "pricing",
            table: "pricing_policies",
            columns: MerchantBranchColumns);

        migrationBuilder.CreateIndex(
            name: "ix_pricing_policies_scope_key_currency_status_priority_effecti",
            schema: "pricing",
            table: "pricing_policies",
            columns: ScopeLookupColumns);

        migrationBuilder.CreateIndex(
            name: "ix_pricing_policies_status_currency_effective_from_utc_effecti",
            schema: "pricing",
            table: "pricing_policies",
            columns: EffectiveLookupColumns);

        migrationBuilder.CreateIndex(
            name: "ix_pricing_rules_pricing_policy_id_type_priority",
            schema: "pricing",
            table: "pricing_rules",
            columns: RuleLookupColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "pricing_rules",
            schema: "pricing");

        migrationBuilder.DropTable(
            name: "pricing_policies",
            schema: "pricing");
    }
}
