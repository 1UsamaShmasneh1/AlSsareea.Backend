using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

namespace AlSsareea.Modules.Maps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MapsDbContext))]
[Migration("20260724170000_InitialMaps")]
public sealed class InitialMaps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:postgis", ",,");

        migrationBuilder.EnsureSchema(name: "maps");

        migrationBuilder.CreateTable(
            name: "service_areas",
            schema: "maps",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(
                    type: "character varying(2000)",
                    maxLength: 2000,
                    nullable: true),
                boundary = table.Column<MultiPolygon>(
                    type: "geometry(MultiPolygon,4326)",
                    nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),
                updated_at_utc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_service_areas", value => value.id);
            });

        migrationBuilder.CreateIndex(
                name: "ix_service_areas_boundary",
                schema: "maps",
                table: "service_areas",
                column: "boundary")
            .Annotation("Npgsql:IndexMethod", "gist");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "service_areas", schema: "maps");
    }
}
