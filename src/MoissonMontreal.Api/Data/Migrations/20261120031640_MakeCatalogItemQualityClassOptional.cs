using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <summary>
    /// Makes the quality class of a catalog item optional (issue #29, review of PR #88).
    /// </summary>
    public partial class MakeCatalogItemQualityClassOptional : Migration
    {
        /// <summary>
        /// Lets a catalog item have no quality class, so items imported from Business Central keep an empty class until someone picks it.
        /// A class that is set must still exist.
        /// </summary>
        /// <param name="migrationBuilder">Builder of the migration steps.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "quality_class",
                table: "catalog_items",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1)",
                oldMaxLength: 1);
        }

        /// <summary>
        /// Makes the quality class required again. A class is never guessed: if a catalog item has no class,
        /// the rollback stops with a clear error and changes nothing, so someone picks the missing classes first.
        /// </summary>
        /// <param name="migrationBuilder">Builder of the rollback steps.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM catalog_items WHERE quality_class IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back MakeCatalogItemQualityClassOptional: some catalog items have no quality class. Pick a class for each of them first.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "quality_class",
                table: "catalog_items",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1)",
                oldMaxLength: 1,
                oldNullable: true);
        }
    }
}
