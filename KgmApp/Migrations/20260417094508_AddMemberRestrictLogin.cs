using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KgmApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberRestrictLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RestrictLogin",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RestrictLogin",
                table: "Members");
        }
    }
}
