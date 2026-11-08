using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Messaging.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Attachment_ContentType",
                schema: "messaging",
                table: "messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attachment_DurationMs",
                schema: "messaging",
                table: "messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Attachment_FileName",
                schema: "messaging",
                table: "messages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Attachment_Kind",
                schema: "messaging",
                table: "messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Attachment_Name",
                schema: "messaging",
                table: "messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Attachment_Size",
                schema: "messaging",
                table: "messages",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attachment_ContentType",
                schema: "messaging",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "Attachment_DurationMs",
                schema: "messaging",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "Attachment_FileName",
                schema: "messaging",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "Attachment_Kind",
                schema: "messaging",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "Attachment_Name",
                schema: "messaging",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "Attachment_Size",
                schema: "messaging",
                table: "messages");
        }
    }
}
