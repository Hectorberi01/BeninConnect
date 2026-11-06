using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Communities.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "communities");

            migrationBuilder.CreateTable(
                name: "communities",
                schema: "communities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CoverImage = table.Column<string>(type: "text", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "join_requests",
                schema: "communities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_join_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_join_requests_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalSchema: "communities",
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "members",
                schema: "communities",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_members", x => new { x.CommunityId, x.UserId });
                    table.ForeignKey(
                        name: "FK_members_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalSchema: "communities",
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_communities_Name",
                schema: "communities",
                table: "communities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_join_requests_CommunityId_UserId",
                schema: "communities",
                table: "join_requests",
                columns: new[] { "CommunityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_members_UserId",
                schema: "communities",
                table: "members",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "join_requests",
                schema: "communities");

            migrationBuilder.DropTable(
                name: "members",
                schema: "communities");

            migrationBuilder.DropTable(
                name: "communities",
                schema: "communities");
        }
    }
}
