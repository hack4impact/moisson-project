using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_food_items_id_donor_id",
                table: "food_items",
                columns: new[] { "id", "donor_id" });

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    donor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    receipt_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "(now() AT TIME ZONE 'America/Montreal')::date"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipts", x => x.id);
                    table.UniqueConstraint("ak_receipts_id_donor_id", x => new { x.id, x.donor_id });
                    table.CheckConstraint("ck_receipts_total_amount_not_negative", "total_amount >= 0");
                    table.ForeignKey(
                        name: "fk_receipts_donors_donor_id",
                        column: x => x.donor_id,
                        principalTable: "donors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_items",
                columns: table => new
                {
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    food_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    donor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    quality_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_items", x => new { x.receipt_id, x.food_item_id });
                    table.CheckConstraint("ck_receipt_items_quality_percent_range", "quality_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_receipt_items_quantity_positive", "quantity > 0");
                    table.CheckConstraint("ck_receipt_items_unit_not_empty", "length(unit) > 0");
                    table.CheckConstraint("ck_receipt_items_unit_price_positive", "unit_price > 0");
                    table.ForeignKey(
                        name: "fk_receipt_items_food_items_food_item_id_donor_id",
                        columns: x => new { x.food_item_id, x.donor_id },
                        principalTable: "food_items",
                        principalColumns: new[] { "id", "donor_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipt_items_receipts_receipt_id_donor_id",
                        columns: x => new { x.receipt_id, x.donor_id },
                        principalTable: "receipts",
                        principalColumns: new[] { "id", "donor_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_receipt_items_food_item_id",
                table: "receipt_items",
                column: "food_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipt_items_food_item_id_donor_id",
                table: "receipt_items",
                columns: new[] { "food_item_id", "donor_id" });

            migrationBuilder.CreateIndex(
                name: "ix_receipt_items_receipt_id_donor_id",
                table: "receipt_items",
                columns: new[] { "receipt_id", "donor_id" });

            migrationBuilder.CreateIndex(
                name: "ix_receipts_donor_id",
                table: "receipts",
                column: "donor_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_items");

            migrationBuilder.DropTable(
                name: "receipts");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_food_items_id_donor_id",
                table: "food_items");
        }
    }
}
