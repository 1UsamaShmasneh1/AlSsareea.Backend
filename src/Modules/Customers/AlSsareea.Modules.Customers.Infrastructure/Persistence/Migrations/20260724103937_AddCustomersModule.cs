using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AlSsareea.Modules.Customers.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCustomersModule : Migration
{
    private static readonly string[] AddressListingColumns = ["customer_id", "created_at_utc", "id"];
    private static readonly string[] HistoryListingColumns = ["customer_id", "changed_at_utc", "id"];
    private static readonly bool[] HistoryListingDescending = [false, true, false];
    private static readonly string[] CustomerCreatedListingColumns = ["created_at_utc", "id"];
    private static readonly string[] CustomerUpdatedListingColumns = ["updated_at_utc", "id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "customers");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:postgis", ",,");

        migrationBuilder.CreateTable(
            name: "customers",
            schema: "customers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                status = table.Column<short>(type: "smallint", nullable: false),
                block_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                blocked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                blocked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customers", x => x.id);
                table.CheckConstraint("ck_customers_block_metadata", "(status = 3 AND block_reason IS NOT NULL AND blocked_at_utc IS NOT NULL AND blocked_by_user_id IS NOT NULL) OR status <> 3");
                table.CheckConstraint("ck_customers_deleted_status", "(status = 4 AND deleted_at_utc IS NOT NULL) OR (status <> 4 AND deleted_at_utc IS NULL)");
                table.CheckConstraint("ck_customers_status", "status BETWEEN 1 AND 4");
            });

        migrationBuilder.CreateTable(
            name: "customer_addresses",
            schema: "customers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                address_type = table.Column<short>(type: "smallint", nullable: false),
                city = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                area = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                building_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                floor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                apartment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                place_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                location = table.Column<Point>(type: "geometry (point, 4326)", nullable: true),
                delivery_instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                is_default = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customer_addresses", x => x.id);
                table.CheckConstraint("ck_customer_addresses_location", "location IS NULL OR ST_SRID(location) = 4326");
                table.CheckConstraint("ck_customer_addresses_type", "address_type BETWEEN 1 AND 3");
                table.ForeignKey(
                    name: "fk_customer_addresses_customers_customer_id",
                    column: x => x.customer_id,
                    principalSchema: "customers",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "customer_preferences",
            schema: "customers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                preferred_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                preferred_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                allow_marketing_notifications = table.Column<bool>(type: "boolean", nullable: false),
                allow_order_status_notifications = table.Column<bool>(type: "boolean", nullable: false),
                allow_promotional_notifications = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customer_preferences", x => x.id);
                table.CheckConstraint("ck_customer_preferences_currency", "preferred_currency ~ '^[A-Z]{3}$'");
                table.CheckConstraint("ck_customer_preferences_language", "preferred_language IN ('ar', 'he', 'en')");
                table.ForeignKey(
                    name: "fk_customer_preferences_customers_customer_id",
                    column: x => x.customer_id,
                    principalSchema: "customers",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "customer_status_history",
            schema: "customers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                previous_status = table.Column<short>(type: "smallint", nullable: false),
                new_status = table.Column<short>(type: "smallint", nullable: false),
                reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customer_status_history", x => x.id);
                table.CheckConstraint("ck_customer_status_history_new_status", "new_status BETWEEN 1 AND 4");
                table.CheckConstraint("ck_customer_status_history_previous_status", "previous_status BETWEEN 1 AND 4");
                table.CheckConstraint("ck_customer_status_history_reason", "new_status = 1 OR reason IS NOT NULL");
                table.ForeignKey(
                    name: "fk_customer_status_history_customers_customer_id",
                    column: x => x.customer_id,
                    principalSchema: "customers",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_customer_addresses_customer_id_created_at_utc_id",
            schema: "customers",
            table: "customer_addresses",
            columns: AddressListingColumns);

        migrationBuilder.CreateIndex(
            name: "ix_customer_addresses_location_gist",
            schema: "customers",
            table: "customer_addresses",
            column: "location")
            .Annotation("Npgsql:IndexMethod", "gist");

        migrationBuilder.CreateIndex(
            name: "ux_customer_addresses_active_default",
            schema: "customers",
            table: "customer_addresses",
            column: "customer_id",
            unique: true,
            filter: "is_default = true AND deleted_at_utc IS NULL");

        migrationBuilder.CreateIndex(
            name: "ux_customer_preferences_customer_id",
            schema: "customers",
            table: "customer_preferences",
            column: "customer_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_customer_status_history_customer_id_changed_at_utc_id",
            schema: "customers",
            table: "customer_status_history",
            columns: HistoryListingColumns,
            descending: HistoryListingDescending);

        migrationBuilder.CreateIndex(
            name: "ix_customers_created_at_utc_id",
            schema: "customers",
            table: "customers",
            columns: CustomerCreatedListingColumns);

        migrationBuilder.CreateIndex(
            name: "ix_customers_status",
            schema: "customers",
            table: "customers",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_customers_updated_at_utc_id",
            schema: "customers",
            table: "customers",
            columns: CustomerUpdatedListingColumns);

        migrationBuilder.CreateIndex(
            name: "ux_customers_user_id",
            schema: "customers",
            table: "customers",
            column: "user_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "customer_addresses",
            schema: "customers");

        migrationBuilder.DropTable(
            name: "customer_preferences",
            schema: "customers");

        migrationBuilder.DropTable(
            name: "customer_status_history",
            schema: "customers");

        migrationBuilder.DropTable(
            name: "customers",
            schema: "customers");
    }
}
