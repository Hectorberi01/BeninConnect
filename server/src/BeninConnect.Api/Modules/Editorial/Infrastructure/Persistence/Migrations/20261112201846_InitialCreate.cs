using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Editorial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "editorial");

            migrationBuilder.CreateTable(
                name: "content_candidates",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceExcerpt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CollectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DraftContent = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    DraftAttempts = table.Column<int>(type: "integer", nullable: false),
                    TargetCommunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Images = table.Column<List<string>>(type: "text[]", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PublishedPostId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sources",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CollectedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_candidates_SourceHash",
                schema: "editorial",
                table: "content_candidates",
                column: "SourceHash",
                unique: true,
                filter: "\"SourceHash\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_content_candidates_Status_CollectedAt",
                schema: "editorial",
                table: "content_candidates",
                columns: new[] { "Status", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_content_candidates_Status_ScheduledAt",
                schema: "editorial",
                table: "content_candidates",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sources_Url",
                schema: "editorial",
                table: "sources",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_candidates",
                schema: "editorial");

            migrationBuilder.DropTable(
                name: "sources",
                schema: "editorial");
        }
    }
}
