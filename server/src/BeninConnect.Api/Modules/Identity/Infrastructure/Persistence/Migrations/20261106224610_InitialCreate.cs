using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LastName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Profession = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    OriginCity = table.Column<string>(type: "text", nullable: false),
                    Bio = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Interests = table.Column<List<string>>(type: "text[]", nullable: false),
                    AvatarColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    CoverImage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AvatarImage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerificationCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    PasswordResetCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    PasswordResetExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PasswordResetAttempts = table.Column<int>(type: "integer", nullable: false),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    SuspensionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SuspendedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                schema: "identity",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
