using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Voyages.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoyagesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "voyages");

            migrationBuilder.CreateTable(
                name: "reports",
                schema: "voyages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Handled = table.Column<bool>(type: "boolean", nullable: false),
                    Decision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                schema: "voyages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AboutTraveler = table.Column<bool>(type: "boolean", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    HiddenReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "route_alerts",
                schema: "voyages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    FromCity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FromCityKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ToCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ToCity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ToCityKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    After = table.Column<DateOnly>(type: "date", nullable: true),
                    Before = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trips",
                schema: "voyages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    FromCity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FromCityKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ToCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ToCity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ToCityKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DepartureDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CapacityKg = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    PricePerKg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AcceptedCategories = table.Column<string[]>(type: "text[]", nullable: false),
                    DropOffZones = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HiddenReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HiddenNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReminderSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "voyages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CertifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeliveryCodeHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    CodeIssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeclineReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    StatusBeforeDispute = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DisputeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisputedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DisputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bookings_trips_TripId",
                        column: x => x.TripId,
                        principalSchema: "voyages",
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_SenderId_CreatedAt",
                schema: "voyages",
                table: "bookings",
                columns: new[] { "SenderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_Status",
                schema: "voyages",
                table: "bookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_TravelerId_Status",
                schema: "voyages",
                table: "bookings",
                columns: new[] { "TravelerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_TripId",
                schema: "voyages",
                table: "bookings",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_Handled_CreatedAt",
                schema: "voyages",
                table: "reports",
                columns: new[] { "Handled", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reports_TargetType_TargetId_AuthorId",
                schema: "voyages",
                table: "reports",
                columns: new[] { "TargetType", "TargetId", "AuthorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_BookingId_AuthorId",
                schema: "voyages",
                table: "reviews",
                columns: new[] { "BookingId", "AuthorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_TargetId_CreatedAt",
                schema: "voyages",
                table: "reviews",
                columns: new[] { "TargetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reviews_TripId",
                schema: "voyages",
                table: "reviews",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_route_alerts_FromCountry_ToCountry",
                schema: "voyages",
                table: "route_alerts",
                columns: new[] { "FromCountry", "ToCountry" });

            migrationBuilder.CreateIndex(
                name: "IX_route_alerts_UserId",
                schema: "voyages",
                table: "route_alerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_trips_FromCountry_ToCountry_DepartureDate",
                schema: "voyages",
                table: "trips",
                columns: new[] { "FromCountry", "ToCountry", "DepartureDate" },
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_trips_Status_DepartureDate",
                schema: "voyages",
                table: "trips",
                columns: new[] { "Status", "DepartureDate" });

            migrationBuilder.CreateIndex(
                name: "IX_trips_TravelerId_CreatedAt",
                schema: "voyages",
                table: "trips",
                columns: new[] { "TravelerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookings",
                schema: "voyages");

            migrationBuilder.DropTable(
                name: "reports",
                schema: "voyages");

            migrationBuilder.DropTable(
                name: "reviews",
                schema: "voyages");

            migrationBuilder.DropTable(
                name: "route_alerts",
                schema: "voyages");

            migrationBuilder.DropTable(
                name: "trips",
                schema: "voyages");
        }
    }
}
