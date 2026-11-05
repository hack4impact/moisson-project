using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateDonorsAndFoodItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "donors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    contact_person = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_donors", x => x.id);
                    table.CheckConstraint("ck_donors_name_not_empty", "length(name) > 0");
                });

            migrationBuilder.CreateTable(
                name: "food_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    donor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    donation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    quality = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_food_items", x => x.id);
                    table.CheckConstraint("ck_food_items_name_not_empty", "length(name) > 0");
                    table.CheckConstraint("ck_food_items_quality", "quality IN ('A', 'B', 'C')");
                    table.CheckConstraint("ck_food_items_quantity_positive", "quantity > 0");
                    table.CheckConstraint("ck_food_items_unit_not_empty", "length(unit) > 0");
                    table.ForeignKey(
                        name: "fk_food_items_donors_donor_id",
                        column: x => x.donor_id,
                        principalTable: "donors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_food_items_donor_id",
                table: "food_items",
                column: "donor_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_items");

            migrationBuilder.DropTable(
                name: "donors");
        }
    }
}
