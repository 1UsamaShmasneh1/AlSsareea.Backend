using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Tracking.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class HardenTrackingLatestConstraints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "ck_driver_latest_locations_accuracy",
            schema: "tracking",
            table: "driver_latest_locations",
            sql: "accuracy_meters > 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_driver_latest_locations_heading",
            schema: "tracking",
            table: "driver_latest_locations",
            sql: "heading_degrees IS NULL OR (heading_degrees >= 0 AND heading_degrees < 360)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_driver_latest_locations_sequence",
            schema: "tracking",
            table: "driver_latest_locations",
            sql: "last_sequence_number >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_driver_latest_locations_speed",
            schema: "tracking",
            table: "driver_latest_locations",
            sql: "speed_meters_per_second IS NULL OR speed_meters_per_second >= 0");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_driver_latest_locations_accuracy",
            schema: "tracking",
            table: "driver_latest_locations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_driver_latest_locations_heading",
            schema: "tracking",
            table: "driver_latest_locations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_driver_latest_locations_sequence",
            schema: "tracking",
            table: "driver_latest_locations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_driver_latest_locations_speed",
            schema: "tracking",
            table: "driver_latest_locations");
    }
}
