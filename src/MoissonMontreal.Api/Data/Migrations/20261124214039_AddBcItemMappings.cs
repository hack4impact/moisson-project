using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <summary>
    /// Adds the Business Central item mappings, their change history and the active flag of catalog items (issue #73).
    /// </summary>
    public partial class AddBcItemMappings : Migration
    {
        /// <summary>
        /// Creates the bc_item_mappings and bc_item_mapping_changes tables and adds is_active to catalog_items.
        /// Existing catalog items become active.
        /// </summary>
        /// <param name="migrationBuilder">Builder of the migration steps.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "bc_item_mapping_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    business_central_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    old_catalog_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_catalog_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bc_item_mapping_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_bc_item_mapping_changes_catalog_items_new_catalog_item_id",
                        column: x => x.new_catalog_item_id,
                        principalTable: "catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bc_item_mapping_changes_catalog_items_old_catalog_item_id",
                        column: x => x.old_catalog_item_id,
                        principalTable: "catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bc_item_mapping_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bc_item_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    business_central_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bc_item_mappings", x => x.id);
                    table.CheckConstraint("ck_bc_item_mappings_business_central_code_not_empty", "length(business_central_code) > 0");
                    table.ForeignKey(
                        name: "fk_bc_item_mappings_catalog_items_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bc_item_mappings_users_confirmed_by_user_id",
                        column: x => x.confirmed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bc_item_mapping_changes_business_central_code",
                table: "bc_item_mapping_changes",
                column: "business_central_code");

            migrationBuilder.CreateIndex(
                name: "ix_bc_item_mapping_changes_changed_by_user_id",
                table: "bc_item_mapping_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_bc_item_mapping_changes_new_catalog_item_id",
                table: "bc_item_mapping_changes",
                column: "new_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_bc_item_mapping_changes_old_catalog_item_id",
                table: "bc_item_mapping_changes",
                column: "old_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_bc_item_mappings_business_central_code",
                table: "bc_item_mappings",
                column: "business_central_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bc_item_mappings_catalog_item_id",
                table: "bc_item_mappings",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_bc_item_mappings_confirmed_by_user_id",
                table: "bc_item_mappings",
                column: "confirmed_by_user_id");
        }

        /// <summary>
        /// Removes the two mapping tables and the is_active column of catalog_items.
        /// </summary>
        /// <param name="migrationBuilder">Builder of the migration steps.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bc_item_mapping_changes");

            migrationBuilder.DropTable(
                name: "bc_item_mappings");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "catalog_items");
        }
    }
}
