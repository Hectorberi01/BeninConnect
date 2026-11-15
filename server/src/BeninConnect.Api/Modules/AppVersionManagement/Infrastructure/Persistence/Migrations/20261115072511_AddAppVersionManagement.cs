using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.AppVersionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppVersionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app_versions");

            migrationBuilder.CreateTable(
                name: "campaigns",
                schema: "app_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false),
                    SendPush = table.Column<bool>(type: "boolean", nullable: false),
                    SendEmail = table.Column<bool>(type: "boolean", nullable: false),
                    DecidedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "installations",
                schema: "app_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Version = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    BuildNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OsVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PushToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "releases",
                schema: "app_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LatestVersion = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    MinimumSupportedVersion = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    StoreUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_releases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "campaign_recipients",
                schema: "app_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrentVersion = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TargetVersion = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_recipients_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "app_versions",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_recipients_CampaignId_Status",
                schema: "app_versions",
                table: "campaign_recipients",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_recipients_UserId_Status_NotifiedAt",
                schema: "app_versions",
                table: "campaign_recipients",
                columns: new[] { "UserId", "Status", "NotifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_Status_CreatedAt",
                schema: "app_versions",
                table: "campaigns",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_installations_AppCode_LastSeenAt",
                schema: "app_versions",
                table: "installations",
                columns: new[] { "AppCode", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_installations_InstallationId_AppCode",
                schema: "app_versions",
                table: "installations",
                columns: new[] { "InstallationId", "AppCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_installations_UserId",
                schema: "app_versions",
                table: "installations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_releases_AppCode_Platform",
                schema: "app_versions",
                table: "releases",
                columns: new[] { "AppCode", "Platform" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_recipients",
                schema: "app_versions");

            migrationBuilder.DropTable(
                name: "installations",
                schema: "app_versions");

            migrationBuilder.DropTable(
                name: "releases",
                schema: "app_versions");

            migrationBuilder.DropTable(
                name: "campaigns",
                schema: "app_versions");
        }
    }
}
