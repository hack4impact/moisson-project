using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateQualityClasses : Migration
    {
        /// <summary>
        /// Creates quality_classes and links food_items to it.
        /// The InsertData of the three initial classes is written by hand, not generated from HasData,
        /// so later migrations never overwrite the values the admin changes. Keep it if this migration is regenerated.
        /// </summary>
        /// <param name="migrationBuilder">Builder used to describe the operations.</param>
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
                    table.CheckConstraint("ck_quality_classes_code_format", "code ~ '^[A-Z]$'");
                    table.CheckConstraint("ck_quality_classes_name_not_empty", "length(name) > 0");
                    table.CheckConstraint("ck_quality_classes_percentage_in_range", "percentage BETWEEN min_percentage AND max_percentage");
                    table.CheckConstraint("ck_quality_classes_range_valid", "min_percentage >= 0 AND min_percentage <= max_percentage AND max_percentage <= 100");
                });

            migrationBuilder.InsertData(
                table: "quality_classes",
                columns: new[] { "code", "name", "percentage", "min_percentage", "max_percentage" },
                values: new object[,]
                {
                    { "A", "Grocery quality", 100m, 100m, 100m },
                    { "B", "Imperfect", 50m, 50m, 75m },
                    { "C", "Near expiry", 0m, 0m, 0m }
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

        /// <summary>
        /// Removes quality_classes and puts back the A, B or C check on food_items.
        /// The first step is written by hand: it stops the rollback with a clear error, before anything is changed,
        /// if a food item uses a class other than A, B or C, or if a class was added or changed,
        /// because dropping the table would lose those values. Keep it if this migration is regenerated.
        /// </summary>
        /// <param name="migrationBuilder">Builder used to describe the operations.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM food_items WHERE quality NOT IN ('A', 'B', 'C')) THEN
                        RAISE EXCEPTION 'Cannot roll back CreateQualityClasses: some food items use a quality class other than A, B or C. Move them to A, B or C first.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM quality_classes
                        WHERE (code, name, percentage, min_percentage, max_percentage) NOT IN (
                            ('A', 'Grocery quality', 100, 100, 100),
                            ('B', 'Imperfect', 50, 50, 75),
                            ('C', 'Near expiry', 0, 0, 0))) THEN
                        RAISE EXCEPTION 'Cannot roll back CreateQualityClasses: quality classes were added or changed since the migration, and rolling back would delete them. Put back the three initial classes first.';
                    END IF;
                END $$;
                """);

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
