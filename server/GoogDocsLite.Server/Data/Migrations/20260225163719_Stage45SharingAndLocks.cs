using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogDocsLite.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage45SharingAndLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentEditLocks",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LockOwnerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    LockOwnerDisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    AcquiredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastHeartbeatAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentEditLocks", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentEditLocks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InviteeEmailNormalized = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcceptedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentInvites_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    GrantedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentPermissions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInvites_DocumentId",
                table: "DocumentInvites",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInvites_InviteeEmailNormalized_Status",
                table: "DocumentInvites",
                columns: new[] { "InviteeEmailNormalized", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentPermissions_DocumentId_UserId",
                table: "DocumentPermissions",
                columns: new[] { "DocumentId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentEditLocks");

            migrationBuilder.DropTable(
                name: "DocumentInvites");

            migrationBuilder.DropTable(
                name: "DocumentPermissions");
        }
    }
}
