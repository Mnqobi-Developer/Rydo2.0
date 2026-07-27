using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated EF migration uses inline composite-index arrays.

#nullable disable

namespace Rydo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "driver_availability",
                columns: table => new
                {
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    LocationUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_availability", x => x.DriverUserId);
                    table.CheckConstraint("CK_driver_availability_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_driver_availability_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "FK_driver_availability_users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickupDistanceKilometres = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    OfferedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_offers", x => x.Id);
                    table.CheckConstraint("CK_trip_offers_Expiry", "\"ExpiresAt\" > \"OfferedAt\"");
                    table.CheckConstraint("CK_trip_offers_PickupDistanceKilometres", "\"PickupDistanceKilometres\" >= 0");
                    table.ForeignKey(
                        name: "FK_trip_offers_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trip_offers_users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_driver_availability_IsOnline_LocationUpdatedAt",
                table: "driver_availability",
                columns: new[] { "IsOnline", "LocationUpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_trip_offers_DriverUserId_Status_ExpiresAt",
                table: "trip_offers",
                columns: new[] { "DriverUserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_trip_offers_TripId_DriverUserId",
                table: "trip_offers",
                columns: new[] { "TripId", "DriverUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_offers_TripId_Status",
                table: "trip_offers",
                columns: new[] { "TripId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "driver_availability");

            migrationBuilder.DropTable(
                name: "trip_offers");
        }
    }
}

#pragma warning restore CA1861
