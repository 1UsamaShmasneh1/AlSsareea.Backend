using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AlSsareea.Modules.Merchants.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddMerchantsModule : Migration
    {
        private static readonly string[] OverrideDateColumns = ["branch_id", "start_date", "end_date"];
        private static readonly string[] BranchCodeColumns = ["merchant_id", "code"];
        private static readonly string[] BusinessHourPeriodColumns = ["business_hour_id", "opens_at"];
        private static readonly string[] BusinessHourDayColumns = ["branch_id", "day_of_week"];
        private static readonly string[] MembershipUserColumns = ["merchant_id", "user_id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "merchants");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "merchants",
                schema: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    registration_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tax_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suspended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suspension_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    rejected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closing_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchants", x => x.id);
                    table.CheckConstraint("ck_merchants_status", "status BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "merchant_branches",
                schema: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    address_city = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address_area = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_building_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    temporarily_closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reopened_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suspended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status_change_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_branches", x => x.id);
                    table.CheckConstraint("ck_merchant_branches_status", "status BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_merchant_branches_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "merchants",
                        principalTable: "merchants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "merchant_branch_schedule_overrides",
                schema: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_branch_schedule_overrides", x => x.id);
                    table.CheckConstraint("ck_schedule_override_dates", "end_date >= start_date");
                    table.ForeignKey(
                        name: "fk_merchant_branch_schedule_overrides_merchant_branches_branch",
                        column: x => x.branch_id,
                        principalSchema: "merchants",
                        principalTable: "merchant_branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "merchant_branch_service_areas",
                schema: "merchants",
                columns: table => new
                {
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_branch_service_areas", x => new { x.branch_id, x.service_area_id });
                    table.ForeignKey(
                        name: "fk_merchant_branch_service_areas_merchant_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "merchants",
                        principalTable: "merchant_branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "merchant_business_hours",
                schema: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<short>(type: "smallint", nullable: false),
                    closed_all_day = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_business_hours", x => x.id);
                    table.CheckConstraint("ck_business_hours_day", "day_of_week BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "fk_merchant_business_hours_merchant_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "merchants",
                        principalTable: "merchant_branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "merchant_employees",
                schema: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    joined_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suspended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    removed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_employees", x => x.id);
                    table.CheckConstraint("ck_merchant_employees_role", "role BETWEEN 1 AND 4");
                    table.CheckConstraint("ck_merchant_employees_status", "status BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "fk_merchant_employees_merchant_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "merchants",
                        principalTable: "merchant_branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_merchant_employees_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "merchants",
                        principalTable: "merchants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "merchant_branch_special_hour_periods",
                schema: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_override_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_branch_special_hour_periods", x => x.id);
                    table.CheckConstraint("ck_special_hour_period_order", "opens_at < closes_at");
                    table.ForeignKey(
                        name: "fk_merchant_branch_special_hour_periods_merchant_branch_schedu",
                        column: x => x.schedule_override_id,
                        principalSchema: "merchants",
                        principalTable: "merchant_branch_schedule_overrides",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merchant_business_hour_periods",
                schema: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_hour_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_business_hour_periods", x => x.id);
                    table.CheckConstraint("ck_business_hour_period_order", "opens_at < closes_at");
                    table.ForeignKey(
                        name: "fk_merchant_business_hour_periods_merchant_business_hours_busi",
                        column: x => x.business_hour_id,
                        principalSchema: "merchants",
                        principalTable: "merchant_business_hours",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_merchant_branch_schedule_overrides_branch_id_start_date_end",
                schema: "merchants",
                table: "merchant_branch_schedule_overrides",
                columns: OverrideDateColumns);

            migrationBuilder.CreateIndex(
                name: "ix_merchant_branch_service_areas_service_area_id",
                schema: "merchants",
                table: "merchant_branch_service_areas",
                column: "service_area_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_branch_special_hour_periods_schedule_override_id",
                schema: "merchants",
                table: "merchant_branch_special_hour_periods",
                column: "schedule_override_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_branches_location_gist",
                schema: "merchants",
                table: "merchant_branches",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_branches_status",
                schema: "merchants",
                table: "merchant_branches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_merchant_branches_merchant_id_code",
                schema: "merchants",
                table: "merchant_branches",
                columns: BranchCodeColumns,
                unique: true,
                filter: "code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_merchant_branches_primary_per_merchant",
                schema: "merchants",
                table: "merchant_branches",
                column: "merchant_id",
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_business_hour_periods_business_hour_id_opens_at",
                schema: "merchants",
                table: "merchant_business_hour_periods",
                columns: BusinessHourPeriodColumns);

            migrationBuilder.CreateIndex(
                name: "ix_merchant_business_hours_branch_id_day_of_week",
                schema: "merchants",
                table: "merchant_business_hours",
                columns: BusinessHourDayColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_merchant_employees_branch_id",
                schema: "merchants",
                table: "merchant_employees",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_employees_user_id",
                schema: "merchants",
                table: "merchant_employees",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_merchant_employees_active_owner",
                schema: "merchants",
                table: "merchant_employees",
                column: "merchant_id",
                unique: true,
                filter: "role = 1 AND status = 2");

            migrationBuilder.CreateIndex(
                name: "ux_merchant_employees_active_user",
                schema: "merchants",
                table: "merchant_employees",
                columns: MembershipUserColumns,
                unique: true,
                filter: "status <> 4");

            migrationBuilder.CreateIndex(
                name: "ix_merchants_display_name",
                schema: "merchants",
                table: "merchants",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "ix_merchants_owner_user_id",
                schema: "merchants",
                table: "merchants",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchants_status",
                schema: "merchants",
                table: "merchants",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "merchant_branch_service_areas",
                schema: "merchants");

            migrationBuilder.DropTable(
                name: "merchant_branch_special_hour_periods",
                schema: "merchants");

            migrationBuilder.DropTable(
                name: "merchant_business_hour_periods",
                schema: "merchants");

            migrationBuilder.DropTable(
                name: "merchant_employees",
                schema: "merchants");

            migrationBuilder.DropTable(
                name: "merchant_branch_schedule_overrides",
                schema: "merchants");

            migrationBuilder.DropTable(
                name: "merchant_business_hours",
                schema: "merchants");

            migrationBuilder.DropTable(
                name: "merchant_branches",
                schema: "merchants");

            migrationBuilder.DropTable(
                name: "merchants",
                schema: "merchants");
        }
}
