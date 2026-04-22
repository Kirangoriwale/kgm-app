using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KgmApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Transactions");
        }
    }
}
