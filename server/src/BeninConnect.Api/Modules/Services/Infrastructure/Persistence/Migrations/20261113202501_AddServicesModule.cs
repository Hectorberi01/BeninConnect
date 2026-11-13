using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Services.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServicesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "services");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Icon = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresQualification = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "favorites",
                schema: "services",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_favorites", x => new { x.UserId, x.ProviderId });
                });

            migrationBuilder.CreateTable(
                name: "offerings",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    PriceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PriceAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Images = table.Column<List<string>>(type: "text[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    HiddenReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AlertKeyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModerationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offerings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provider_documents",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "providers",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Bio = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    PhotoPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PhoneIsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    ContactPreference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LegalStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Siret = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    Ifu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    ServiceRadiusKm = table.Column<int>(type: "integer", nullable: false),
                    ServesAtHome = table.Column<bool>(type: "boolean", nullable: false),
                    HasPremises = table.Column<bool>(type: "boolean", nullable: false),
                    QualificationDeclared = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerificationLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HiddenReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AlertKeyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    RatingAverage = table.Column<double>(type: "double precision", nullable: false),
                    RatingCount = table.Column<int>(type: "integer", nullable: false),
                    ResponseRate = table.Column<double>(type: "double precision", nullable: false),
                    MedianResponseMinutes = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Handled = table.Column<bool>(type: "boolean", nullable: false),
                    Decision = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "requests",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PreferredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeclineReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ViewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewReminderSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderReply = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RepliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HiddenReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModerationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AlertKeywords = table.Column<List<string>>(type: "text[]", nullable: false),
                    ReportThreshold = table.Column<int>(type: "integer", nullable: false),
                    DailyRequestLimit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_ParentId",
                schema: "services",
                table: "categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_categories_Slug",
                schema: "services",
                table: "categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_favorites_ProviderId",
                schema: "services",
                table: "favorites",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_offerings_HiddenReason_CategoryId",
                schema: "services",
                table: "offerings",
                columns: new[] { "HiddenReason", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_offerings_ProviderId",
                schema: "services",
                table: "offerings",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_documents_ProviderId",
                schema: "services",
                table: "provider_documents",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_documents_Status_UploadedAt",
                schema: "services",
                table: "provider_documents",
                columns: new[] { "Status", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_providers_Status_City",
                schema: "services",
                table: "providers",
                columns: new[] { "Status", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_providers_Status_Latitude_Longitude",
                schema: "services",
                table: "providers",
                columns: new[] { "Status", "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_providers_UserId",
                schema: "services",
                table: "providers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_Handled_CreatedAt",
                schema: "services",
                table: "reports",
                columns: new[] { "Handled", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reports_TargetType_TargetId_AuthorId",
                schema: "services",
                table: "reports",
                columns: new[] { "TargetType", "TargetId", "AuthorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_requests_ClientId_CreatedAt",
                schema: "services",
                table: "requests",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_requests_ProviderId_Status",
                schema: "services",
                table: "requests",
                columns: new[] { "ProviderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_requests_ProviderUserId_CreatedAt",
                schema: "services",
                table: "requests",
                columns: new[] { "ProviderUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_requests_Status_CompletedAt",
                schema: "services",
                table: "requests",
                columns: new[] { "Status", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reviews_AuthorId",
                schema: "services",
                table: "reviews",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ProviderId_Status_CreatedAt",
                schema: "services",
                table: "reviews",
                columns: new[] { "ProviderId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reviews_RequestId",
                schema: "services",
                table: "reviews",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categories",
                schema: "services");

            migrationBuilder.DropTable(
                name: "favorites",
                schema: "services");

            migrationBuilder.DropTable(
                name: "offerings",
                schema: "services");

            migrationBuilder.DropTable(
                name: "provider_documents",
                schema: "services");

            migrationBuilder.DropTable(
                name: "providers",
                schema: "services");

            migrationBuilder.DropTable(
                name: "reports",
                schema: "services");

            migrationBuilder.DropTable(
                name: "requests",
                schema: "services");

            migrationBuilder.DropTable(
                name: "reviews",
                schema: "services");

            migrationBuilder.DropTable(
                name: "settings",
                schema: "services");
        }
    }
}
