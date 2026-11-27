using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <summary>
    /// Adds the history of the changes made to employees' accounts, and the row version of users.
    /// </summary>
    public partial class AddUserAccountChanges : Migration
    {
        /// <summary>
        /// Creates the user_account_changes table. The row version of users uses the PostgreSQL system column xmin,
        /// so the generated SQL adds no column to users.
        /// </summary>
        /// <param name="migrationBuilder">Builder of the migration steps.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "users",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "user_account_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    old_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    new_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    old_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    new_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    old_role_id = table.Column<int>(type: "integer", nullable: true),
                    new_role_id = table.Column<int>(type: "integer", nullable: true),
                    old_is_active = table.Column<bool>(type: "boolean", nullable: true),
                    new_is_active = table.Column<bool>(type: "boolean", nullable: true),
                    changed_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_account_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_account_changes_roles_new_role_id",
                        column: x => x.new_role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_account_changes_roles_old_role_id",
                        column: x => x.old_role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_account_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_account_changes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_account_changes_changed_by_user_id",
                table: "user_account_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_account_changes_new_role_id",
                table: "user_account_changes",
                column: "new_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_account_changes_old_role_id",
                table: "user_account_changes",
                column: "old_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_account_changes_user_id",
                table: "user_account_changes",
                column: "user_id");
        }

        /// <summary>
        /// Removes the user_account_changes table.
        /// </summary>
        /// <param name="migrationBuilder">Builder of the migration steps.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_account_changes");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "users");
        }
    }
}
