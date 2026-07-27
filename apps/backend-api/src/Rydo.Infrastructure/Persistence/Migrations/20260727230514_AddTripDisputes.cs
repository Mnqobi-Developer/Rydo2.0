using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Generated EF migration uses inline composite-index arrays.

namespace Rydo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disputes", x => x.Id);
                    table.CheckConstraint("CK_disputes_ResolutionState", "(\"Status\" IN ('Open', 'UnderReview') AND \"ResolvedAt\" IS NULL AND \"ResolvedByUserId\" IS NULL AND \"Resolution\" IS NULL) OR (\"Status\" IN ('Resolved', 'Rejected') AND \"ResolvedAt\" IS NOT NULL AND \"ResolvedByUserId\" IS NOT NULL AND \"Resolution\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_disputes_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_disputes_users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_disputes_users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispute_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisputeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispute_messages_disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispute_messages_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispute_messages_AuthorUserId",
                table: "dispute_messages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_messages_DisputeId_CreatedAt",
                table: "dispute_messages",
                columns: new[] { "DisputeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_disputes_OpenedByUserId",
                table: "disputes",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_ResolvedByUserId",
                table: "disputes",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_Status_UpdatedAt",
                table: "disputes",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_disputes_TripId",
                table: "disputes",
                column: "TripId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispute_messages");

            migrationBuilder.DropTable(
                name: "disputes");
        }
    }
}

#pragma warning restore CA1861
