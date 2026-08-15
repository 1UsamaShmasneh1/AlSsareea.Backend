using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Drivers.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class HardenDriversPhase13 : Migration
{
    private static readonly string[] OutboxPendingColumns = ["processed_at_utc", "created_at_utc"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_driver_outbox_pending",
            schema: "drivers",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "ux_driver_suspensions_active",
            schema: "drivers",
            table: "driver_suspensions");

        migrationBuilder.AddColumn<string>(
            name: "response_json",
            schema: "drivers",
            table: "idempotency_records",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<short>(
            name: "response_status",
            schema: "drivers",
            table: "idempotency_records",
            type: "smallint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_driver_outbox_pending",
            schema: "drivers",
            table: "outbox_messages",
            columns: OutboxPendingColumns,
            filter: "processed_at_utc IS NULL");

        migrationBuilder.AddCheckConstraint(
            name: "ck_driver_outbox_attempts",
            schema: "drivers",
            table: "outbox_messages",
            sql: "attempt_count >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_driver_outbox_event_type",
            schema: "drivers",
            table: "outbox_messages",
            sql: "char_length(event_type) > 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_driver_outbox_payload",
            schema: "drivers",
            table: "outbox_messages",
            sql: "jsonb_typeof(payload) = 'object'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_driver_idempotency_response",
            schema: "drivers",
            table: "idempotency_records",
            sql: "response_json IS NULL OR jsonb_typeof(response_json) = 'object'");

        migrationBuilder.CreateIndex(
            name: "ix_driver_suspensions_driver_id",
            schema: "drivers",
            table: "driver_suspensions",
            column: "driver_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_driver_outbox_pending",
            schema: "drivers",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_driver_outbox_attempts",
            schema: "drivers",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_driver_outbox_event_type",
            schema: "drivers",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_driver_outbox_payload",
            schema: "drivers",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_driver_idempotency_response",
            schema: "drivers",
            table: "idempotency_records");

        migrationBuilder.DropIndex(
            name: "ix_driver_suspensions_driver_id",
            schema: "drivers",
            table: "driver_suspensions");

        migrationBuilder.DropColumn(
            name: "response_json",
            schema: "drivers",
            table: "idempotency_records");

        migrationBuilder.DropColumn(
            name: "response_status",
            schema: "drivers",
            table: "idempotency_records");

        migrationBuilder.CreateIndex(
            name: "ix_driver_outbox_pending",
            schema: "drivers",
            table: "outbox_messages",
            columns: OutboxPendingColumns);

        migrationBuilder.CreateIndex(
            name: "ux_driver_suspensions_active",
            schema: "drivers",
            table: "driver_suspensions",
            column: "driver_id",
            unique: true,
            filter: "status = 1");
    }
}
