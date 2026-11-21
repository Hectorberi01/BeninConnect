using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Comptes existants : ville affichée et messages autorisés, comme avant
            migrationBuilder.AddColumn<bool>(
                name: "AllowMessages",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PrivateProfile",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowCity",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowMessages",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PrivateProfile",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ShowCity",
                schema: "identity",
                table: "users");
        }
    }
}
