using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItemDetails : Migration
    {
        /// <summary>
        /// Adds the catalog item details. The UPDATE that fills existing rows is written by hand, not generated:
        /// without it, rows that already exist would keep empty values and fail the checks, the unique index and the
        /// foreign key added below. Keep it if this migration is regenerated.
        /// </summary>
        /// <param name="migrationBuilder">Builder used to describe the operations.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_central_code",
                table: "catalog_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "catalog_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "quality_class",
                table: "catalog_items",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "unit",
                table: "catalog_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "catalog_items",
                type: "timestamp with time zone",
                nullable: true);

            // Items created before this migration get placeholder values to fix by hand. The id keeps each code unique.
            migrationBuilder.Sql("""
                UPDATE catalog_items
                SET category = 'To classify',
                    business_central_code = 'TO-FIX-' || id::text,
                    unit = 'unit',
                    quality_class = 'A';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "business_central_code",
                table: "catalog_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "catalog_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "quality_class",
                table: "catalog_items",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1)",
                oldMaxLength: 1,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "unit",
                table: "catalog_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_items_business_central_code",
                table: "catalog_items",
                column: "business_central_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_items_quality_class",
                table: "catalog_items",
                column: "quality_class");

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalog_items_business_central_code_not_empty",
                table: "catalog_items",
                sql: "length(business_central_code) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalog_items_category_not_empty",
                table: "catalog_items",
                sql: "length(category) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalog_items_unit_not_empty",
                table: "catalog_items",
                sql: "length(unit) > 0");

            migrationBuilder.AddForeignKey(
                name: "fk_catalog_items_quality_classes_quality_class",
                table: "catalog_items",
                column: "quality_class",
                principalTable: "quality_classes",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_catalog_items_quality_classes_quality_class",
                table: "catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_catalog_items_business_central_code",
                table: "catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_catalog_items_quality_class",
                table: "catalog_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_catalog_items_business_central_code_not_empty",
                table: "catalog_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_catalog_items_category_not_empty",
                table: "catalog_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_catalog_items_unit_not_empty",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "business_central_code",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "category",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "quality_class",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "unit",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "catalog_items");
        }
    }
}
