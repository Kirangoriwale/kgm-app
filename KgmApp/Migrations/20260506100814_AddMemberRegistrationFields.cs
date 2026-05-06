using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KgmApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberRegistrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AadhaarNo",
                table: "Members",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessOrJob",
                table: "Members",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Education",
                table: "Members",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Terms",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TermsAcceptYN",
                table: "Members",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AadhaarNo",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "BusinessOrJob",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Education",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Terms",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "TermsAcceptYN",
                table: "Members");
        }
    }
}
