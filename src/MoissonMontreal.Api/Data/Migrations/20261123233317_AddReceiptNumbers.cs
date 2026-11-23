using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptNumbers : Migration
    {
        /// <summary>
        /// Adds receipt numbers. The UPDATE statements that number the receipts already issued are written by hand, not generated:
        /// without them, an issued receipt created before this migration would fail the check added below. Keep them if this
        /// migration is regenerated.
        /// </summary>
        /// <param name="migrationBuilder">Builder used to describe the operations.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "receipt_number",
                table: "receipts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "receipt_number_counters",
                columns: table => new
                {
                    series = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_number_counters", x => x.series);
                    table.CheckConstraint("ck_receipt_number_counters_last_number_in_range", "last_number BETWEEN 0 AND 999999");
                });

            migrationBuilder.InsertData(
                table: "receipt_number_counters",
                columns: new[] { "series", "last_number" },
                values: new object[] { "DEN", 0 });

            migrationBuilder.Sql("""
                UPDATE receipts AS r
                SET receipt_number = 'DEN_' || lpad(numbered.position::text, 6, '0')
                FROM (
                    SELECT id, row_number() OVER (ORDER BY created_at, id) AS position
                    FROM receipts
                    WHERE status = 'Issued'
                ) AS numbered
                WHERE r.id = numbered.id;

                UPDATE receipt_number_counters
                SET last_number = (SELECT count(*) FROM receipts WHERE receipt_number IS NOT NULL)
                WHERE series = 'DEN';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_receipts_receipt_number",
                table: "receipts",
                column: "receipt_number",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipts_issued_has_receipt_number",
                table: "receipts",
                sql: "status <> 'Issued' OR receipt_number IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipts_receipt_number_format",
                table: "receipts",
                sql: "receipt_number ~ '^DEN_[0-9]{6}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipts_receipt_number_only_once_issued",
                table: "receipts",
                sql: "status IN ('Issued', 'Cancelled') OR receipt_number IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_number_counters");

            migrationBuilder.DropIndex(
                name: "ix_receipts_receipt_number",
                table: "receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipts_issued_has_receipt_number",
                table: "receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipts_receipt_number_format",
                table: "receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipts_receipt_number_only_once_issued",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "receipt_number",
                table: "receipts");
        }
    }
}
