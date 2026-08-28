using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace AlSsareea.Modules.Dispatching.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitializeDispatchingModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "dispatching");

        migrationBuilder.CreateTable(
            name: "dispatch_audit",
            schema: "dispatching",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                dispatch_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                old_status = table.Column<short>(type: "smallint", nullable: false),
                new_status = table.Column<short>(type: "smallint", nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                idempotency_key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_dispatch_audit", x => x.id);
                table.CheckConstraint("ck_dispatch_audit_key_hash", "char_length(idempotency_key_hash) = 64");
            });

        migrationBuilder.CreateTable(
            name: "dispatch_idempotency_records",
            schema: "dispatching",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                dispatch_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_dispatch_idempotency_records", x => x.id);
                table.CheckConstraint("ck_dispatch_idempotency_hashes", "char_length(key_hash) = 64 AND char_length(request_hash) = 64");
            });

        migrationBuilder.CreateTable(
            name: "dispatch_outbox_messages",
            schema: "dispatching",
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
                table.PrimaryKey("pk_dispatch_outbox_messages", x => x.id);
                table.CheckConstraint("ck_dispatch_outbox_attempts", "attempt_count >= 0");
                table.CheckConstraint("ck_dispatch_outbox_payload", "jsonb_typeof(payload) = 'object'");
            });

        migrationBuilder.CreateTable(
            name: "dispatch_requests",
            schema: "dispatching",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                pickup_latitude = table.Column<double>(type: "double precision", nullable: false),
                pickup_longitude = table.Column<double>(type: "double precision", nullable: false),
                required_vehicle_type = table.Column<short>(type: "smallint", nullable: true),
                preparation_seconds = table.Column<int>(type: "integer", nullable: true),
                status = table.Column<short>(type: "smallint", nullable: false),
                attempt_number = table.Column<int>(type: "integer", nullable: false),
                assigned_driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_dispatch_requests", x => x.id);
                table.CheckConstraint("ck_dispatch_requests_assignment", "status <> 4 OR assigned_driver_id IS NOT NULL AND completed_at_utc IS NOT NULL");
                table.CheckConstraint("ck_dispatch_requests_attempt", "attempt_number >= 0");
                table.CheckConstraint("ck_dispatch_requests_coordinates", "pickup_latitude BETWEEN -90 AND 90 AND pickup_longitude BETWEEN -180 AND 180");
                table.CheckConstraint("ck_dispatch_requests_status", "status BETWEEN 1 AND 6");
            });

        migrationBuilder.CreateTable(
            name: "dispatch_candidates",
            schema: "dispatching",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                dispatch_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                attempt_number = table.Column<int>(type: "integer", nullable: false),
                distance_meters = table.Column<long>(type: "bigint", nullable: false),
                eta_seconds = table.Column<int>(type: "integer", nullable: false),
                current_load = table.Column<int>(type: "integer", nullable: false),
                maximum_capacity = table.Column<int>(type: "integer", nullable: false),
                last_assignment_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                score = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                rank = table.Column<int>(type: "integer", nullable: false),
                explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_dispatch_candidates", x => x.id);
                table.CheckConstraint("ck_dispatch_candidates_metrics", "distance_meters >= 0 AND eta_seconds >= 0 AND current_load >= 0 AND maximum_capacity > 0");
                table.CheckConstraint("ck_dispatch_candidates_rank", "rank > 0");
                table.ForeignKey(
                    name: "fk_dispatch_candidates_dispatch_requests_dispatch_request_id",
                    column: x => x.dispatch_request_id,
                    principalSchema: "dispatching",
                    principalTable: "dispatch_requests",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "dispatch_history",
            schema: "dispatching",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                dispatch_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                attempt_number = table.Column<int>(type: "integer", nullable: false),
                type = table.Column<short>(type: "smallint", nullable: false),
                offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_dispatch_history", x => x.id);
                table.ForeignKey(
                    name: "fk_dispatch_history_dispatch_requests_dispatch_request_id",
                    column: x => x.dispatch_request_id,
                    principalSchema: "dispatching",
                    principalTable: "dispatch_requests",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "dispatch_offers",
            schema: "dispatching",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                dispatch_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                attempt_number = table.Column<int>(type: "integer", nullable: false),
                sequence = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                offered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                responded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                decline_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_dispatch_offers", x => x.id);
                table.CheckConstraint("ck_dispatch_offers_expiry", "expires_at_utc > offered_at_utc");
                table.CheckConstraint("ck_dispatch_offers_response", "status = 1 AND responded_at_utc IS NULL OR status <> 1 AND responded_at_utc IS NOT NULL");
                table.CheckConstraint("ck_dispatch_offers_status", "status BETWEEN 1 AND 6");
                table.ForeignKey(
                    name: "fk_dispatch_offers_dispatch_requests_dispatch_request_id",
                    column: x => x.dispatch_request_id,
                    principalSchema: "dispatching",
                    principalTable: "dispatch_requests",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_dispatch_audit_request_occurred",
            schema: "dispatching",
            table: "dispatch_audit",
            columns: new[] { "dispatch_request_id", "occurred_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ux_dispatch_candidates_request_attempt_driver",
            schema: "dispatching",
            table: "dispatch_candidates",
            columns: new[] { "dispatch_request_id", "attempt_number", "driver_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_dispatch_candidates_request_attempt_rank",
            schema: "dispatching",
            table: "dispatch_candidates",
            columns: new[] { "dispatch_request_id", "attempt_number", "rank" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_dispatch_history_request_occurred",
            schema: "dispatching",
            table: "dispatch_history",
            columns: new[] { "dispatch_request_id", "occurred_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ux_dispatch_idempotency_actor_operation_key",
            schema: "dispatching",
            table: "dispatch_idempotency_records",
            columns: new[] { "actor_id", "operation", "key_hash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_dispatch_offers_active_expiry",
            schema: "dispatching",
            table: "dispatch_offers",
            column: "expires_at_utc",
            filter: "status = 1");

        migrationBuilder.CreateIndex(
            name: "ux_dispatch_offers_request_attempt_driver",
            schema: "dispatching",
            table: "dispatch_offers",
            columns: new[] { "dispatch_request_id", "attempt_number", "driver_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_dispatch_offers_request_sequence",
            schema: "dispatching",
            table: "dispatch_offers",
            columns: new[] { "dispatch_request_id", "sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_dispatch_outbox_pending",
            schema: "dispatching",
            table: "dispatch_outbox_messages",
            columns: new[] { "processed_at_utc", "occurred_at_utc" },
            filter: "processed_at_utc IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_dispatch_requests_assigned_driver_id",
            schema: "dispatching",
            table: "dispatch_requests",
            column: "assigned_driver_id");

        migrationBuilder.CreateIndex(
            name: "ix_dispatch_requests_status_updated",
            schema: "dispatching",
            table: "dispatch_requests",
            columns: new[] { "status", "updated_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ux_dispatch_requests_delivery_id",
            schema: "dispatching",
            table: "dispatch_requests",
            column: "delivery_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "dispatch_audit",
            schema: "dispatching");

        migrationBuilder.DropTable(
            name: "dispatch_candidates",
            schema: "dispatching");

        migrationBuilder.DropTable(
            name: "dispatch_history",
            schema: "dispatching");

        migrationBuilder.DropTable(
            name: "dispatch_idempotency_records",
            schema: "dispatching");

        migrationBuilder.DropTable(
            name: "dispatch_offers",
            schema: "dispatching");

        migrationBuilder.DropTable(
            name: "dispatch_outbox_messages",
            schema: "dispatching");

        migrationBuilder.DropTable(
            name: "dispatch_requests",
            schema: "dispatching");
    }
}
