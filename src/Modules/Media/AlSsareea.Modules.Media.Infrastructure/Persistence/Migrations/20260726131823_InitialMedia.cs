using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Media.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialMedia : Migration
{
    private static readonly string[] MerchantStatusColumns = ["merchant_id", "status"];
    private static readonly string[] OwnerColumns = ["owner_type", "owner_id"];
    private static readonly string[] AssetVariantColumns = ["media_asset_id", "type"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "media");

        migrationBuilder.CreateTable(
            name: "media_assets",
            schema: "media",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                file_extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                size_in_bytes = table.Column<long>(type: "bigint", nullable: false),
                content_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                width = table.Column<int>(type: "integer", nullable: false),
                height = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                access_level = table.Column<short>(type: "smallint", nullable: false),
                storage_provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_media_assets", x => x.id);
                table.CheckConstraint("ck_media_assets_access", "access_level BETWEEN 1 AND 3");
                table.CheckConstraint("ck_media_assets_dimensions", "width > 0 AND height > 0");
                table.CheckConstraint("ck_media_assets_hash", "length(content_hash) = 64");
                table.CheckConstraint("ck_media_assets_size", "size_in_bytes > 0");
                table.CheckConstraint("ck_media_assets_status", "status BETWEEN 1 AND 5");
            });

        migrationBuilder.CreateTable(
            name: "media_variants",
            schema: "media",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<short>(type: "smallint", nullable: false),
                storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                size_in_bytes = table.Column<long>(type: "bigint", nullable: false),
                width = table.Column<int>(type: "integer", nullable: false),
                height = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_media_variants", x => x.id);
                table.CheckConstraint("ck_media_variants_dimensions", "width > 0 AND height > 0");
                table.CheckConstraint("ck_media_variants_size", "size_in_bytes > 0");
                table.ForeignKey(
                    name: "fk_media_variants_media_assets_media_asset_id",
                    column: x => x.media_asset_id,
                    principalSchema: "media",
                    principalTable: "media_assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_media_assets_content_hash",
            schema: "media",
            table: "media_assets",
            column: "content_hash");

        migrationBuilder.CreateIndex(
            name: "ix_media_assets_created_at_utc",
            schema: "media",
            table: "media_assets",
            column: "created_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_media_assets_deleted_at_utc",
            schema: "media",
            table: "media_assets",
            column: "deleted_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_media_assets_merchant_id_status",
            schema: "media",
            table: "media_assets",
            columns: MerchantStatusColumns);

        migrationBuilder.CreateIndex(
            name: "ix_media_assets_owner_type_owner_id",
            schema: "media",
            table: "media_assets",
            columns: OwnerColumns);

        migrationBuilder.CreateIndex(
            name: "ix_media_assets_storage_key",
            schema: "media",
            table: "media_assets",
            column: "storage_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_media_variants_media_asset_id_type",
            schema: "media",
            table: "media_variants",
            columns: AssetVariantColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_media_variants_storage_key",
            schema: "media",
            table: "media_variants",
            column: "storage_key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "media_variants",
            schema: "media");

        migrationBuilder.DropTable(
            name: "media_assets",
            schema: "media");
    }
}
