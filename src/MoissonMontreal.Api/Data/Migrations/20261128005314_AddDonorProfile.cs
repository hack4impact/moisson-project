using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoissonMontreal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDonorProfile : Migration
    {
        /// <summary>
        /// Adds the donor profile columns. The default of the type column is written by hand, not generated:
        /// it gives the donors that already exist the type organization, then it is removed so a new donor must state its type.
        /// Keep it if this migration is regenerated.
        /// </summary>
        /// <param name="migrationBuilder">Builder used to describe the operations.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_central_code",
                table: "donors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "donors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "donors",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "donors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "donors",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "province",
                table: "donors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "donors",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "organization");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "donors",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "organization");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "donors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_donors_business_central_code",
                table: "donors",
                column: "business_central_code",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_donors_business_central_code_not_empty",
                table: "donors",
                sql: "length(business_central_code) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_donors_type",
                table: "donors",
                sql: "type IN ('individual', 'organization')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_donors_business_central_code",
                table: "donors");

            migrationBuilder.DropCheckConstraint(
                name: "ck_donors_business_central_code_not_empty",
                table: "donors");

            migrationBuilder.DropCheckConstraint(
                name: "ck_donors_type",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "business_central_code",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "city",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "email",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "province",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "type",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "donors");
        }
    }
}
