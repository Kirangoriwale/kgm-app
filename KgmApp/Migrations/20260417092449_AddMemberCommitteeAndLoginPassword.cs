using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KgmApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberCommitteeAndLoginPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCommiteeMember",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LoginPassword",
                table: "Members",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCommiteeMember",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "LoginPassword",
                table: "Members");
        }
    }
}
