using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateCatalogItemsAndPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "catalog_item_id",
                table: "food_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catalog_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_items", x => x.id);
                    table.CheckConstraint("ck_catalog_items_name_not_empty", "length(name) > 0");
                });

            migrationBuilder.CreateTable(
                name: "prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prices", x => x.id);
                    table.CheckConstraint("ck_prices_unit_price_positive", "unit_price > 0");
                    table.ForeignKey(
                        name: "fk_prices_catalog_items_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_food_items_catalog_item_id",
                table: "food_items",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_prices_catalog_item_id_effective_date",
                table: "prices",
                columns: new[] { "catalog_item_id", "effective_date" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_food_items_catalog_items_catalog_item_id",
                table: "food_items",
                column: "catalog_item_id",
                principalTable: "catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_food_items_catalog_items_catalog_item_id",
                table: "food_items");

            migrationBuilder.DropTable(
                name: "prices");

            migrationBuilder.DropTable(
                name: "catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_food_items_catalog_item_id",
                table: "food_items");

            migrationBuilder.DropColumn(
                name: "catalog_item_id",
                table: "food_items");
        }
    }
}
