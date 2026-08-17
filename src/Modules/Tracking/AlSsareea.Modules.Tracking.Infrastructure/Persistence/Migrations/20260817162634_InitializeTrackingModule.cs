using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AlSsareea.Modules.Tracking.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitializeTrackingModule : Migration
{
    private static readonly string[] DriverRecordedColumns = ["driver_id", "recorded_at_utc"];
    private static readonly bool[] DriverRecordedDescending = [false, true];
    private static readonly string[] DriverSequenceColumns = ["driver_id", "sequence_number"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "tracking");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:postgis", ",,");

        migrationBuilder.CreateTable(
            name: "driver_latest_locations",
            schema: "tracking",
            columns: table => new
            {
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                location_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                accuracy_meters = table.Column<double>(type: "double precision", nullable: false),
                speed_meters_per_second = table.Column<double>(type: "double precision", nullable: true),
                heading_degrees = table.Column<double>(type: "double precision", nullable: true),
                last_sequence_number = table.Column<long>(type: "bigint", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_latest_locations", x => x.driver_id);
                table.CheckConstraint("ck_driver_latest_locations_position_srid", "ST_SRID(position) = 4326");
            });

        migrationBuilder.CreateTable(
            name: "driver_locations",
            schema: "tracking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                accuracy_meters = table.Column<double>(type: "double precision", nullable: false),
                speed_meters_per_second = table.Column<double>(type: "double precision", nullable: true),
                heading_degrees = table.Column<double>(type: "double precision", nullable: true),
                altitude_meters = table.Column<double>(type: "double precision", nullable: true),
                sequence_number = table.Column<long>(type: "bigint", nullable: false),
                source = table.Column<short>(type: "smallint", nullable: false),
                batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_driver_locations", x => x.id);
                table.CheckConstraint("ck_driver_locations_accuracy", "accuracy_meters > 0");
                table.CheckConstraint("ck_driver_locations_heading", "heading_degrees IS NULL OR (heading_degrees >= 0 AND heading_degrees < 360)");
                table.CheckConstraint("ck_driver_locations_position_srid", "ST_SRID(position) = 4326");
                table.CheckConstraint("ck_driver_locations_sequence", "sequence_number >= 0");
                table.CheckConstraint("ck_driver_locations_speed", "speed_meters_per_second IS NULL OR speed_meters_per_second >= 0");
            });

        migrationBuilder.CreateIndex(
            name: "ix_driver_latest_locations_position",
            schema: "tracking",
            table: "driver_latest_locations",
            column: "position")
            .Annotation("Npgsql:IndexMethod", "gist");

        migrationBuilder.CreateIndex(
            name: "ix_driver_latest_locations_recorded_at_utc",
            schema: "tracking",
            table: "driver_latest_locations",
            column: "recorded_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_driver_locations_driver_id_recorded_at_utc",
            schema: "tracking",
            table: "driver_locations",
            columns: DriverRecordedColumns,
            descending: DriverRecordedDescending);

        migrationBuilder.CreateIndex(
            name: "ix_driver_locations_driver_id_sequence_number",
            schema: "tracking",
            table: "driver_locations",
            columns: DriverSequenceColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_driver_locations_position",
            schema: "tracking",
            table: "driver_locations",
            column: "position")
            .Annotation("Npgsql:IndexMethod", "gist");

        migrationBuilder.CreateIndex(
            name: "ix_driver_locations_received_at_utc",
            schema: "tracking",
            table: "driver_locations",
            column: "received_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_driver_locations_recorded_at_utc",
            schema: "tracking",
            table: "driver_locations",
            column: "recorded_at_utc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "driver_latest_locations",
            schema: "tracking");

        migrationBuilder.DropTable(
            name: "driver_locations",
            schema: "tracking");
    }
}
