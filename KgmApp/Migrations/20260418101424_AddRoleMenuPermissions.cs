using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KgmApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleMenuPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleMenuPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MenuKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanView = table.Column<bool>(type: "boolean", nullable: false),
                    CanAdd = table.Column<bool>(type: "boolean", nullable: false),
                    CanEdit = table.Column<bool>(type: "boolean", nullable: false),
                    CanDelete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleMenuPermissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleMenuPermissions_RoleName_MenuKey",
                table: "RoleMenuPermissions",
                columns: new[] { "RoleName", "MenuKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleMenuPermissions");
        }
    }
}
