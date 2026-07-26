using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Catalog.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCategoryMediaAsset : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "media_asset_id",
            schema: "catalog",
            table: "categories",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_categories_media_asset_id",
            schema: "catalog",
            table: "categories",
            column: "media_asset_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_categories_media_asset_id",
            schema: "catalog",
            table: "categories");

        migrationBuilder.DropColumn(
            name: "media_asset_id",
            schema: "catalog",
            table: "categories");
    }
}
