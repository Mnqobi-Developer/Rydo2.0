using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Rydo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedFareAmount",
                table: "trips",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FareCurrency",
                table: "trips",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FareQuoteId",
                table: "trips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingVersion",
                table: "trips",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RideCategory",
                table: "trips",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fare_quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PassengerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickupLatitude = table.Column<double>(type: "double precision", nullable: false),
                    PickupLongitude = table.Column<double>(type: "double precision", nullable: false),
                    DestinationLatitude = table.Column<double>(type: "double precision", nullable: false),
                    DestinationLongitude = table.Column<double>(type: "double precision", nullable: false),
                    DistanceMeters = table.Column<int>(type: "integer", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    PricingVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DemandMultiplier = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fare_quotes", x => x.Id);
                    table.CheckConstraint("CK_fare_quotes_DemandMultiplier", "\"DemandMultiplier\" BETWEEN 1 AND 1.5");
                    table.CheckConstraint("CK_fare_quotes_DestinationLatitude", "\"DestinationLatitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_fare_quotes_DestinationLongitude", "\"DestinationLongitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_fare_quotes_DistanceMeters", "\"DistanceMeters\" > 0");
                    table.CheckConstraint("CK_fare_quotes_DurationSeconds", "\"DurationSeconds\" > 0");
                    table.CheckConstraint("CK_fare_quotes_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.CheckConstraint("CK_fare_quotes_PickupLatitude", "\"PickupLatitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_fare_quotes_PickupLongitude", "\"PickupLongitude\" BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "FK_fare_quotes_users_PassengerUserId",
                        column: x => x.PassengerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fare_quote_options",
                columns: table => new
                {
                    FareQuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RatePerKilometre = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MinimumFare = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DistanceCharge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MinimumFareAdjustment = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    BookingFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DemandAdjustment = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EstimatedTolls = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    WaitingFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fare_quote_options", x => new { x.FareQuoteId, x.Category });
                    table.CheckConstraint("CK_fare_quote_options_Amounts", "\"DistanceCharge\" >= 0 AND \"MinimumFareAdjustment\" >= 0 AND \"BookingFee\" >= 0 AND \"DemandAdjustment\" >= 0 AND \"EstimatedTolls\" >= 0 AND \"WaitingFee\" >= 0 AND \"Discount\" >= 0");
                    table.CheckConstraint("CK_fare_quote_options_Total", "\"Total\" > 0");
                    table.ForeignKey(
                        name: "FK_fare_quote_options_fare_quotes_FareQuoteId",
                        column: x => x.FareQuoteId,
                        principalTable: "fare_quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trips_FareQuoteId",
                table: "trips",
                column: "FareQuoteId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_trips_EstimatedFareAmount",
                table: "trips",
                sql: "\"EstimatedFareAmount\" IS NULL OR \"EstimatedFareAmount\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_fare_quotes_ExpiresAt",
                table: "fare_quotes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_fare_quotes_PassengerUserId_CreatedAt",
                table: "fare_quotes",
                columns: new[] { "PassengerUserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_trips_fare_quotes_FareQuoteId",
                table: "trips",
                column: "FareQuoteId",
                principalTable: "fare_quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trips_fare_quotes_FareQuoteId",
                table: "trips");

            migrationBuilder.DropTable(
                name: "fare_quote_options");

            migrationBuilder.DropTable(
                name: "fare_quotes");

            migrationBuilder.DropIndex(
                name: "IX_trips_FareQuoteId",
                table: "trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_trips_EstimatedFareAmount",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "EstimatedFareAmount",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "FareCurrency",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "FareQuoteId",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "PricingVersion",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "RideCategory",
                table: "trips");
        }
    }
}
