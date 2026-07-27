using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Generated EF migration uses inline composite-index arrays.

namespace Rydo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "driver_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    StorageObjectKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupersededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_driver_documents_users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_driver_documents_DriverUserId_DocumentType",
                table: "driver_documents",
                columns: new[] { "DriverUserId", "DocumentType" },
                unique: true,
                filter: "\"SupersededAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_driver_documents_StorageObjectKey",
                table: "driver_documents",
                column: "StorageObjectKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "driver_documents");
        }
    }
}
#pragma warning restore CA1861
