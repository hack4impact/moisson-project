using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateQualityClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_food_items_quality",
                table: "food_items");

            migrationBuilder.CreateTable(
                name: "quality_classes",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    min_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    max_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_classes", x => x.code);
                    table.CheckConstraint("ck_quality_classes_code_not_empty", "length(code) > 0");
                    table.CheckConstraint("ck_quality_classes_name_not_empty", "length(name) > 0");
                    table.CheckConstraint("ck_quality_classes_percentage_in_range", "percentage BETWEEN min_percentage AND max_percentage");
                    table.CheckConstraint("ck_quality_classes_range_valid", "min_percentage >= 0 AND min_percentage <= max_percentage AND max_percentage <= 100");
                });

            migrationBuilder.InsertData(
                table: "quality_classes",
                columns: new[] { "code", "max_percentage", "min_percentage", "name", "percentage" },
                values: new object[,]
                {
                    { "A", 100m, 100m, "Grocery quality", 100m },
                    { "B", 75m, 50m, "Imperfect", 50m },
                    { "C", 0m, 0m, "Near expiry", 0m }
                });

            migrationBuilder.CreateIndex(
                name: "ix_food_items_quality",
                table: "food_items",
                column: "quality");

            migrationBuilder.CreateIndex(
                name: "ix_quality_classes_name",
                table: "quality_classes",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_food_items_quality_classes_quality",
                table: "food_items",
                column: "quality",
                principalTable: "quality_classes",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_food_items_quality_classes_quality",
                table: "food_items");

            migrationBuilder.DropTable(
                name: "quality_classes");

            migrationBuilder.DropIndex(
                name: "ix_food_items_quality",
                table: "food_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_items_quality",
                table: "food_items",
                sql: "quality IN ('A', 'B', 'C')");
        }
    }
}
