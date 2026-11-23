using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "receipts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateTable(
                name: "receipt_status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_status_changes", x => x.id);
                    table.CheckConstraint("ck_receipt_status_changes_from_status_valid", "from_status IN ('Draft', 'PendingSupplyApproval', 'PendingFinanceApproval', 'Approved', 'Issued', 'Cancelled')");
                    table.CheckConstraint("ck_receipt_status_changes_reason_not_empty", "length(reason) > 0");
                    table.CheckConstraint("ck_receipt_status_changes_reason_required", "to_status NOT IN ('Draft', 'Cancelled') OR reason IS NOT NULL");
                    table.CheckConstraint("ck_receipt_status_changes_status_differs", "from_status <> to_status");
                    table.CheckConstraint("ck_receipt_status_changes_to_status_valid", "to_status IN ('Draft', 'PendingSupplyApproval', 'PendingFinanceApproval', 'Approved', 'Issued', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_receipt_status_changes_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipt_status_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipts_status_valid",
                table: "receipts",
                sql: "status IN ('Draft', 'PendingSupplyApproval', 'PendingFinanceApproval', 'Approved', 'Issued', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "ix_receipt_status_changes_changed_by_user_id",
                table: "receipt_status_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_receipt_status_changes_receipt_id_changed_at",
                table: "receipt_status_changes",
                columns: new[] { "receipt_id", "changed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_status_changes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipts_status_valid",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "status",
                table: "receipts");
        }
    }
}
