using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Administration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BannersAsImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bannière sans image : plus affichable (l'image porte désormais tout le visuel)
            migrationBuilder.Sql("DELETE FROM administration.banners WHERE \"ImagePath\" IS NULL;");

            migrationBuilder.DropColumn(
                name: "Highlight",
                schema: "administration",
                table: "banners");

            migrationBuilder.DropColumn(
                name: "Layout",
                schema: "administration",
                table: "banners");

            migrationBuilder.DropColumn(
                name: "Subtitle",
                schema: "administration",
                table: "banners");

            migrationBuilder.DropColumn(
                name: "Theme",
                schema: "administration",
                table: "banners");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                schema: "administration",
                table: "banners",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Target",
                schema: "administration",
                table: "banners",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Target",
                schema: "administration",
                table: "banners");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                schema: "administration",
                table: "banners",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "Highlight",
                schema: "administration",
                table: "banners",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Layout",
                schema: "administration",
                table: "banners",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                schema: "administration",
                table: "banners",
                type: "character varying(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Theme",
                schema: "administration",
                table: "banners",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
