using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Drivers.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitializeDriversModule : Migration
{
    private static readonly string[] DriverOccurredColumns = ["driver_id", "occurred_at_utc"];
    private static readonly string[] DriverDocumentTypeColumns = ["driver_id", "type"];
    private static readonly string[] DriverZoneColumns = ["driver_id", "zone_id"];
    private static readonly string[] IdempotencyScopeColumns = ["actor_user_id", "operation", "key_hash"];
    private static readonly string[] OutboxPendingColumns = ["processed_at_utc", "created_at_utc"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "drivers");

        migrationBuilder.CreateTable(
            name: "audit_records",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_audit", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "drivers",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                profile_photo_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<short>(type: "smallint", nullable: false),
                activation_status = table.Column<short>(type: "smallint", nullable: false),
                employment_type = table.Column<short>(type: "smallint", nullable: false),
                availability_status = table.Column<short>(type: "smallint", nullable: false),
                maximum_concurrent_deliveries = table.Column<int>(type: "integer", nullable: false),
                current_load = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                suspended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                archived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_availability_changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_drivers", x => x.id);
                table.CheckConstraint("ck_drivers_activation_status", "activation_status BETWEEN 1 AND 5");
                table.CheckConstraint("ck_drivers_availability_status", "availability_status BETWEEN 1 AND 5");
                table.CheckConstraint("ck_drivers_capacity", "maximum_concurrent_deliveries > 0 AND current_load >= 0 AND current_load <= maximum_concurrent_deliveries");
                table.CheckConstraint("ck_drivers_status", "status BETWEEN 1 AND 6");
            });

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                key_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                request_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_idempotency", x => x.id);
                table.CheckConstraint("ck_driver_idempotency_hashes", "char_length(key_hash) = 64 AND char_length(request_hash) = 64");
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_outbox", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "driver_documents",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<short>(type: "smallint", nullable: false),
                media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_documents", x => x.id);
                table.CheckConstraint("ck_driver_documents_dates", "expires_at_utc IS NULL OR issued_at_utc IS NULL OR expires_at_utc > issued_at_utc");
                table.CheckConstraint("ck_driver_documents_rejection", "status <> 3 OR rejection_reason IS NOT NULL");
                table.CheckConstraint("ck_driver_documents_review", "status NOT IN (2, 3) OR reviewed_at_utc IS NOT NULL");
                table.ForeignKey(
                    name: "fk_driver_documents_drivers_driver_id",
                    column: x => x.driver_id,
                    principalSchema: "drivers",
                    principalTable: "drivers",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "driver_shifts",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                scheduled_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                scheduled_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                actual_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                actual_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                status = table.Column<short>(type: "smallint", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_shifts", x => x.id);
                table.CheckConstraint("ck_driver_shifts_actual", "actual_end_utc IS NULL OR actual_start_utc IS NOT NULL AND actual_end_utc >= actual_start_utc");
                table.CheckConstraint("ck_driver_shifts_scheduled", "scheduled_end_utc > scheduled_start_utc");
                table.ForeignKey(
                    name: "fk_driver_shifts_drivers_driver_id",
                    column: x => x.driver_id,
                    principalSchema: "drivers",
                    principalTable: "drivers",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "driver_suspensions",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                lifted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                lifted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                lift_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                status = table.Column<short>(type: "smallint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_suspensions", x => x.id);
                table.CheckConstraint("ck_driver_suspensions_dates", "ends_at_utc IS NULL OR ends_at_utc > starts_at_utc");
                table.ForeignKey(
                    name: "fk_driver_suspensions_drivers_driver_id",
                    column: x => x.driver_id,
                    principalSchema: "drivers",
                    principalTable: "drivers",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "driver_violations",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                violation_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                severity = table.Column<short>(type: "smallint", nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                resolution_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_violations", x => x.id);
                table.ForeignKey(
                    name: "fk_driver_violations_drivers_driver_id",
                    column: x => x.driver_id,
                    principalSchema: "drivers",
                    principalTable: "drivers",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "driver_zone_assignments",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_primary = table.Column<bool>(type: "boolean", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                removed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_zone_assignments", x => x.id);
                table.ForeignKey(
                    name: "fk_driver_zones_drivers_driver_id",
                    column: x => x.driver_id,
                    principalSchema: "drivers",
                    principalTable: "drivers",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "vehicles",
            schema: "drivers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<short>(type: "smallint", nullable: false),
                make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                year = table.Column<int>(type: "integer", nullable: true),
                color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                plate_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                normalized_plate_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                registration_country = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                is_primary = table.Column<bool>(type: "boolean", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                verified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_vehicles", x => x.id);
                table.CheckConstraint("ck_vehicles_status", "status BETWEEN 1 AND 6");
                table.CheckConstraint("ck_vehicles_type", "type BETWEEN 1 AND 5");
                table.CheckConstraint("ck_vehicles_year", "year IS NULL OR year BETWEEN 1980 AND 2100");
                table.ForeignKey(
                    name: "fk_vehicles_drivers_driver_id",
                    column: x => x.driver_id,
                    principalSchema: "drivers",
                    principalTable: "drivers",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_driver_audit_driver_occurred",
            schema: "drivers",
            table: "audit_records",
            columns: DriverOccurredColumns);

        migrationBuilder.CreateIndex(
            name: "ix_driver_documents_driver_id",
            schema: "drivers",
            table: "driver_documents",
            column: "driver_id");

        migrationBuilder.CreateIndex(
            name: "ix_driver_documents_expires_at_utc",
            schema: "drivers",
            table: "driver_documents",
            column: "expires_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_driver_documents_status",
            schema: "drivers",
            table: "driver_documents",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_driver_documents_type",
            schema: "drivers",
            table: "driver_documents",
            column: "type");

        migrationBuilder.CreateIndex(
            name: "ux_driver_documents_current_type",
            schema: "drivers",
            table: "driver_documents",
            columns: DriverDocumentTypeColumns,
            unique: true,
            filter: "status IN (1, 2)");

        migrationBuilder.CreateIndex(
            name: "ix_driver_shifts_driver_id",
            schema: "drivers",
            table: "driver_shifts",
            column: "driver_id");

        migrationBuilder.CreateIndex(
            name: "ix_driver_shifts_scheduled_start_utc",
            schema: "drivers",
            table: "driver_shifts",
            column: "scheduled_start_utc");

        migrationBuilder.CreateIndex(
            name: "ix_driver_shifts_status",
            schema: "drivers",
            table: "driver_shifts",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_driver_suspensions_ends_at_utc",
            schema: "drivers",
            table: "driver_suspensions",
            column: "ends_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_driver_suspensions_starts_at_utc",
            schema: "drivers",
            table: "driver_suspensions",
            column: "starts_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_driver_suspensions_status",
            schema: "drivers",
            table: "driver_suspensions",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ux_driver_suspensions_active",
            schema: "drivers",
            table: "driver_suspensions",
            column: "driver_id",
            unique: true,
            filter: "status = 1");

        migrationBuilder.CreateIndex(
            name: "ix_driver_violations_driver_id",
            schema: "drivers",
            table: "driver_violations",
            column: "driver_id");

        migrationBuilder.CreateIndex(
            name: "ix_driver_violations_occurred_at_utc",
            schema: "drivers",
            table: "driver_violations",
            column: "occurred_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_driver_violations_severity",
            schema: "drivers",
            table: "driver_violations",
            column: "severity");

        migrationBuilder.CreateIndex(
            name: "ix_driver_violations_status",
            schema: "drivers",
            table: "driver_violations",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_driver_zones_zone_id",
            schema: "drivers",
            table: "driver_zone_assignments",
            column: "zone_id");

        migrationBuilder.CreateIndex(
            name: "ux_driver_zones_active",
            schema: "drivers",
            table: "driver_zone_assignments",
            columns: DriverZoneColumns,
            unique: true,
            filter: "is_active");

        migrationBuilder.CreateIndex(
            name: "ux_driver_zones_primary",
            schema: "drivers",
            table: "driver_zone_assignments",
            column: "driver_id",
            unique: true,
            filter: "is_active AND is_primary");

        migrationBuilder.CreateIndex(
            name: "ix_drivers_activation_status",
            schema: "drivers",
            table: "drivers",
            column: "activation_status");

        migrationBuilder.CreateIndex(
            name: "ix_drivers_availability_status",
            schema: "drivers",
            table: "drivers",
            column: "availability_status");

        migrationBuilder.CreateIndex(
            name: "ix_drivers_created_at_utc",
            schema: "drivers",
            table: "drivers",
            column: "created_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_drivers_employment_type",
            schema: "drivers",
            table: "drivers",
            column: "employment_type");

        migrationBuilder.CreateIndex(
            name: "ix_drivers_status",
            schema: "drivers",
            table: "drivers",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_drivers_updated_at_utc",
            schema: "drivers",
            table: "drivers",
            column: "updated_at_utc");

        migrationBuilder.CreateIndex(
            name: "ux_drivers_user_id",
            schema: "drivers",
            table: "drivers",
            column: "user_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_driver_idempotency_scope",
            schema: "drivers",
            table: "idempotency_records",
            columns: IdempotencyScopeColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_driver_outbox_pending",
            schema: "drivers",
            table: "outbox_messages",
            columns: OutboxPendingColumns);

        migrationBuilder.CreateIndex(
            name: "ix_vehicles_status",
            schema: "drivers",
            table: "vehicles",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ux_vehicles_active_plate",
            schema: "drivers",
            table: "vehicles",
            column: "normalized_plate_number",
            unique: true,
            filter: "normalized_plate_number IS NOT NULL AND status <> 6");

        migrationBuilder.CreateIndex(
            name: "ux_vehicles_driver_primary_active",
            schema: "drivers",
            table: "vehicles",
            column: "driver_id",
            unique: true,
            filter: "is_primary AND status = 2");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_records",
            schema: "drivers");

        migrationBuilder.DropTable(
            name: "driver_documents",
            schema: "drivers");

        migrationBuilder.DropTable(
            name: "driver_shifts",
            schema: "drivers");

        migrationBuilder.DropTable(
            name: "driver_suspensions",
            schema: "drivers");

        migrationBuilder.DropTable(
            name: "driver_violations",
            schema: "drivers");

        migrationBuilder.DropTable(
            name: "driver_zone_assignments",
            schema: "drivers");

        migrationBuilder.DropTable(
            name: "idempotency_records",
            schema: "drivers");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "drivers");

        migrationBuilder.DropTable(
            name: "vehicles",
            schema: "drivers");

        migrationBuilder.DropTable(
            name: "drivers",
            schema: "drivers");
    }
}
