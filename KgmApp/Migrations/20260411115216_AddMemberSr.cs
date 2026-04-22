using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KgmApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberSr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Sr",
                table: "Members",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sr",
                table: "Members");
        }
    }
}
