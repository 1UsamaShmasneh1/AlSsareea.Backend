using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Identity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitializeIdentityPersistence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "identity");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:postgis", ",,");

        migrationBuilder.CreateTable(
            name: "users",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                preferred_language = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_users_phone_number",
            schema: "identity",
            table: "users",
            column: "phone_number");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "users",
            schema: "identity");
    }
}
