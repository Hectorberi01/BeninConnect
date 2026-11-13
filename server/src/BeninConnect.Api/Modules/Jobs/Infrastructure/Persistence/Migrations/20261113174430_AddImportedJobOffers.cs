using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeninConnect.Api.Modules.Jobs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportedJobOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jobs");

            migrationBuilder.CreateTable(
                name: "imported_job_offers",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompanyLogoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    LocationLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Department = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    ContractType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContractLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsRemote = table.Column<bool>(type: "boolean", nullable: false),
                    WorkingTime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SalaryLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SectorLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApplyUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosingAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HiddenReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NeedsReview = table.Column<bool>(type: "boolean", nullable: false),
                    AlertKeyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SearchKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DuplicateOfId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imported_job_offers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AlertKeywords = table.Column<List<string>>(type: "text[]", nullable: false),
                    ReportThreshold = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sources",
                schema: "jobs",
                columns: table => new
                {
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ImportIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EncryptedSecret = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SecretUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Searches = table.Column<string>(type: "jsonb", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastAdded = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<int>(type: "integer", nullable: false),
                    LastDuplicates = table.Column<int>(type: "integer", nullable: false),
                    LastHidden = table.Column<int>(type: "integer", nullable: false),
                    LastExpired = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sources", x => x.Source);
                });

            migrationBuilder.CreateTable(
                name: "job_offer_favorites",
                schema: "jobs",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_offer_favorites", x => new { x.UserId, x.OfferId });
                    table.ForeignKey(
                        name: "FK_job_offer_favorites_imported_job_offers_OfferId",
                        column: x => x.OfferId,
                        principalSchema: "jobs",
                        principalTable: "imported_job_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_offer_reports",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Handled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_offer_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_offer_reports_imported_job_offers_OfferId",
                        column: x => x.OfferId,
                        principalSchema: "jobs",
                        principalTable: "imported_job_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_imported_job_offers_DuplicateOfId",
                schema: "jobs",
                table: "imported_job_offers",
                column: "DuplicateOfId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_job_offers_Fingerprint",
                schema: "jobs",
                table: "imported_job_offers",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_imported_job_offers_NeedsReview",
                schema: "jobs",
                table: "imported_job_offers",
                column: "NeedsReview");

            migrationBuilder.CreateIndex(
                name: "IX_imported_job_offers_Source_ExternalId",
                schema: "jobs",
                table: "imported_job_offers",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_imported_job_offers_Source_Status_LastSeenAt",
                schema: "jobs",
                table: "imported_job_offers",
                columns: new[] { "Source", "Status", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_imported_job_offers_Status_PublishedAt_Number",
                schema: "jobs",
                table: "imported_job_offers",
                columns: new[] { "Status", "PublishedAt", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_job_offer_favorites_OfferId",
                schema: "jobs",
                table: "job_offer_favorites",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_job_offer_reports_OfferId_UserId",
                schema: "jobs",
                table: "job_offer_reports",
                columns: new[] { "OfferId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_offer_reports_UserId",
                schema: "jobs",
                table: "job_offer_reports",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_offer_favorites",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_offer_reports",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "settings",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "sources",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "imported_job_offers",
                schema: "jobs");
        }
    }
}
