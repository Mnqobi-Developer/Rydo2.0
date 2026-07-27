using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rydo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "driver_vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    VehicleIdentificationNumber = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    SeatCapacity = table.Column<int>(type: "integer", nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_vehicles", x => x.Id);
                    table.CheckConstraint("CK_driver_vehicles_SeatCapacity", "\"SeatCapacity\" BETWEEN 1 AND 16");
                    table.CheckConstraint("CK_driver_vehicles_Year", "\"Year\" BETWEEN 1980 AND 2100");
                    table.ForeignKey(
                        name: "FK_driver_vehicles_users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_driver_vehicles_DriverUserId",
                table: "driver_vehicles",
                column: "DriverUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_driver_vehicles_RegistrationNumber",
                table: "driver_vehicles",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_driver_vehicles_VehicleIdentificationNumber",
                table: "driver_vehicles",
                column: "VehicleIdentificationNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "driver_vehicles");
        }
    }
}
