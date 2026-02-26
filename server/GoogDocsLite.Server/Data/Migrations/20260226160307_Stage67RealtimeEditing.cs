using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogDocsLite.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage67RealtimeEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentDeltaJson",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LiveRevision",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "DocumentOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClientOpId = table.Column<string>(type: "TEXT", nullable: false),
                    DeltaJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentOperations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOperations_DocumentId_CreatedAtUtc",
                table: "DocumentOperations",
                columns: new[] { "DocumentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOperations_DocumentId_Revision",
                table: "DocumentOperations",
                columns: new[] { "DocumentId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOperations_DocumentId_UserId_ClientOpId",
                table: "DocumentOperations",
                columns: new[] { "DocumentId", "UserId", "ClientOpId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentOperations");

            migrationBuilder.DropColumn(
                name: "ContentDeltaJson",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LiveRevision",
                table: "Documents");
        }
    }
}
