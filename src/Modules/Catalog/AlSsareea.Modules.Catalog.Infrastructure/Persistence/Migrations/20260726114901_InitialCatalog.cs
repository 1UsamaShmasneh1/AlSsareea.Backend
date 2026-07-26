using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlSsareea.Modules.Catalog.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCatalog : Migration
{
    private static readonly string[] CategorySortColumns = ["merchant_id", "catalog_id", "sort_order"];
    private static readonly string[] AvailabilityColumns = ["product_id", "merchant_branch_id", "day_of_week"];
    private static readonly string[] MerchantSkuColumns = ["merchant_id", "sku"];
    private static readonly string[] ProductFilterColumns =
        ["merchant_id", "status", "inventory_status", "is_visible", "sort_order"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "catalog");

        migrationBuilder.CreateTable(
            name: "catalogs",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                default_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_catalogs", x => x.id);
                table.CheckConstraint("ck_catalogs_status", "status BETWEEN 1 AND 4");
            });

        migrationBuilder.CreateTable(
            name: "categories",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                catalog_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_visible = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_categories", x => x.id);
                table.CheckConstraint("ck_categories_sort_order", "sort_order >= 0");
                table.ForeignKey(
                    name: "fk_categories_categories_parent_category_id",
                    column: x => x.parent_category_id,
                    principalSchema: "catalog",
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "menu_sections",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                catalog_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_visible = table.Column<bool>(type: "boolean", nullable: false),
                available_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                available_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_menu_sections", x => x.id);
                table.CheckConstraint("ck_menu_sections_sort_order", "sort_order >= 0");
            });

        migrationBuilder.CreateTable(
            name: "products",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                catalog_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                category_id = table.Column<Guid>(type: "uuid", nullable: true),
                sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                base_price_minor = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                tax_category_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                status = table.Column<short>(type: "smallint", nullable: false),
                inventory_status = table.Column<short>(type: "smallint", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_visible = table.Column<bool>(type: "boolean", nullable: false),
                is_featured = table.Column<bool>(type: "boolean", nullable: false),
                current_version = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_products", x => x.id);
                table.CheckConstraint("ck_products_price", "base_price_minor >= 0");
                table.CheckConstraint("ck_products_sort", "sort_order >= 0");
                table.CheckConstraint("ck_products_version", "current_version >= 1");
            });

        migrationBuilder.CreateTable(
            name: "category_translations",
            schema: "catalog",
            columns: table => new
            {
                category_id = table.Column<Guid>(type: "uuid", nullable: false),
                language_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                search_text = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_category_translations", x => new { x.category_id, x.language_code });
                table.ForeignKey(
                    name: "fk_category_translations_categories_category_id",
                    column: x => x.category_id,
                    principalSchema: "catalog",
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "menu_section_products",
            schema: "catalog",
            columns: table => new
            {
                menu_section_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_menu_section_products", x => new { x.menu_section_id, x.product_id });
                table.CheckConstraint("ck_menu_section_products_sort_order", "sort_order >= 0");
                table.ForeignKey(
                    name: "fk_menu_section_products_menu_sections_menu_section_id",
                    column: x => x.menu_section_id,
                    principalSchema: "catalog",
                    principalTable: "menu_sections",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "menu_section_translations",
            schema: "catalog",
            columns: table => new
            {
                menu_section_id = table.Column<Guid>(type: "uuid", nullable: false),
                language_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_menu_section_translations", x => new { x.menu_section_id, x.language_code });
                table.ForeignKey(
                    name: "fk_menu_section_translations_menu_sections_menu_section_id",
                    column: x => x.menu_section_id,
                    principalSchema: "catalog",
                    principalTable: "menu_sections",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "option_groups",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                selection_type = table.Column<short>(type: "smallint", nullable: false),
                is_required = table.Column<bool>(type: "boolean", nullable: false),
                min_selections = table.Column<int>(type: "integer", nullable: false),
                max_selections = table.Column<int>(type: "integer", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_visible = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_option_groups", x => x.id);
                table.CheckConstraint("ck_option_groups_limits", "min_selections >= 0 AND max_selections >= 1 AND min_selections <= max_selections");
                table.CheckConstraint("ck_option_groups_sort", "sort_order >= 0");
                table.ForeignKey(
                    name: "fk_option_groups_products_product_id",
                    column: x => x.product_id,
                    principalSchema: "catalog",
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "product_availability_schedules",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                day_of_week = table.Column<short>(type: "smallint", nullable: false),
                start_local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                end_local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                is_enabled = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_product_availability_schedules", x => x.id);
                table.ForeignKey(
                    name: "fk_product_availability_schedules_products_product_id",
                    column: x => x.product_id,
                    principalSchema: "catalog",
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "product_image_references",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                media_id = table.Column<Guid>(type: "uuid", nullable: true),
                external_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                alt_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_primary = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_product_image_references", x => x.id);
                table.CheckConstraint("ck_product_images_reference", "media_id IS NOT NULL OR external_reference IS NOT NULL");
                table.ForeignKey(
                    name: "fk_product_image_references_products_product_id",
                    column: x => x.product_id,
                    principalSchema: "catalog",
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "product_translations",
            schema: "catalog",
            columns: table => new
            {
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                language_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                search_text = table.Column<string>(type: "character varying(4200)", maxLength: 4200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_product_translations", x => new { x.product_id, x.language_code });
                table.ForeignKey(
                    name: "fk_product_translations_products_product_id",
                    column: x => x.product_id,
                    principalSchema: "catalog",
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "product_variants",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                price_adjustment_minor = table.Column<long>(type: "bigint", nullable: false),
                inventory_status = table.Column<short>(type: "smallint", nullable: false),
                is_default = table.Column<bool>(type: "boolean", nullable: false),
                is_visible = table.Column<bool>(type: "boolean", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_product_variants", x => x.id);
                table.CheckConstraint("ck_product_variants_sort", "sort_order >= 0");
                table.ForeignKey(
                    name: "fk_product_variants_products_product_id",
                    column: x => x.product_id,
                    principalSchema: "catalog",
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "option_group_translations",
            schema: "catalog",
            columns: table => new
            {
                option_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                language_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_option_group_translations", x => new { x.option_group_id, x.language_code });
                table.ForeignKey(
                    name: "fk_option_group_translations_option_groups_option_group_id",
                    column: x => x.option_group_id,
                    principalSchema: "catalog",
                    principalTable: "option_groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "product_options",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                option_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                price_adjustment_minor = table.Column<long>(type: "bigint", nullable: false),
                is_default = table.Column<bool>(type: "boolean", nullable: false),
                is_available = table.Column<bool>(type: "boolean", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_product_options", x => x.id);
                table.CheckConstraint("ck_product_options_sort", "sort_order >= 0");
                table.ForeignKey(
                    name: "fk_product_options_option_groups_option_group_id",
                    column: x => x.option_group_id,
                    principalSchema: "catalog",
                    principalTable: "option_groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "product_variant_translations",
            schema: "catalog",
            columns: table => new
            {
                product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                language_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_product_variant_translations", x => new { x.product_variant_id, x.language_code });
                table.ForeignKey(
                    name: "fk_product_variant_translations_product_variants_product_varia",
                    column: x => x.product_variant_id,
                    principalSchema: "catalog",
                    principalTable: "product_variants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "product_option_translations",
            schema: "catalog",
            columns: table => new
            {
                product_option_id = table.Column<Guid>(type: "uuid", nullable: false),
                language_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_product_option_translations", x => new { x.product_option_id, x.language_code });
                table.ForeignKey(
                    name: "fk_product_option_translations_product_options_product_option_",
                    column: x => x.product_option_id,
                    principalSchema: "catalog",
                    principalTable: "product_options",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_catalogs_merchant_id",
            schema: "catalog",
            table: "catalogs",
            column: "merchant_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_categories_merchant_id_catalog_id_sort_order",
            schema: "catalog",
            table: "categories",
            columns: CategorySortColumns);

        migrationBuilder.CreateIndex(
            name: "ix_categories_parent_category_id",
            schema: "catalog",
            table: "categories",
            column: "parent_category_id");

        migrationBuilder.CreateIndex(
            name: "ix_category_translations_search_text",
            schema: "catalog",
            table: "category_translations",
            column: "search_text");

        migrationBuilder.CreateIndex(
            name: "ix_option_groups_product_id",
            schema: "catalog",
            table: "option_groups",
            column: "product_id");

        migrationBuilder.CreateIndex(
            name: "ix_product_availability_schedules_product_id_merchant_branch_i",
            schema: "catalog",
            table: "product_availability_schedules",
            columns: AvailabilityColumns);

        migrationBuilder.CreateIndex(
            name: "ix_product_image_references_product_id",
            schema: "catalog",
            table: "product_image_references",
            column: "product_id",
            unique: true,
            filter: "is_primary = true");

        migrationBuilder.CreateIndex(
            name: "ix_product_options_option_group_id",
            schema: "catalog",
            table: "product_options",
            column: "option_group_id");

        migrationBuilder.CreateIndex(
            name: "ix_product_translations_search_text",
            schema: "catalog",
            table: "product_translations",
            column: "search_text");

        migrationBuilder.CreateIndex(
            name: "ix_product_variants_merchant_id_sku",
            schema: "catalog",
            table: "product_variants",
            columns: MerchantSkuColumns,
            unique: true,
            filter: "sku IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_product_variants_product_id",
            schema: "catalog",
            table: "product_variants",
            column: "product_id",
            unique: true,
            filter: "is_default = true");

        migrationBuilder.CreateIndex(
            name: "ix_products_merchant_id_sku",
            schema: "catalog",
            table: "products",
            columns: MerchantSkuColumns,
            unique: true,
            filter: "sku IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_products_merchant_id_status_inventory_status_is_visible_sor",
            schema: "catalog",
            table: "products",
            columns: ProductFilterColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "catalogs",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "category_translations",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "menu_section_products",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "menu_section_translations",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "option_group_translations",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "product_availability_schedules",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "product_image_references",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "product_option_translations",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "product_translations",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "product_variant_translations",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "categories",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "menu_sections",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "product_options",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "product_variants",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "option_groups",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "products",
            schema: "catalog");
    }
}
