using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KgmApp.Migrations
{
    /// <inheritdoc />
    public partial class AddSubMemberSrNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SrNo",
                table: "SubMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SrNo",
                table: "SubMembers");
        }
    }
}
