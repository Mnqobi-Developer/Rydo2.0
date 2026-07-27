using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated EF migration uses an inline composite-index array.

#nullable disable

namespace Rydo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripsStateMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PassengerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PickupAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PickupLatitude = table.Column<double>(type: "double precision", nullable: false),
                    PickupLongitude = table.Column<double>(type: "double precision", nullable: false),
                    DestinationAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DestinationLatitude = table.Column<double>(type: "double precision", nullable: false),
                    DestinationLongitude = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DriverArrivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trips", x => x.Id);
                    table.CheckConstraint("CK_trips_DestinationLatitude", "\"DestinationLatitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_trips_DestinationLongitude", "\"DestinationLongitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_trips_PickupLatitude", "\"PickupLatitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_trips_PickupLongitude", "\"PickupLongitude\" BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "FK_trips_users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trips_users_PassengerUserId",
                        column: x => x.PassengerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trips_DriverUserId",
                table: "trips",
                column: "DriverUserId",
                unique: true,
                filter: "\"DriverUserId\" IS NOT NULL AND \"Status\" IN ('Accepted', 'DriverArrived', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_trips_PassengerUserId",
                table: "trips",
                column: "PassengerUserId",
                unique: true,
                filter: "\"Status\" IN ('Requested', 'Accepted', 'DriverArrived', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_trips_Status_RequestedAt",
                table: "trips",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trips");
        }
    }
}

#pragma warning restore CA1861
