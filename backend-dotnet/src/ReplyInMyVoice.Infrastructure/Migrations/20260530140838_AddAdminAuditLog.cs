using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminExternalAuthUserId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AdminEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_Action_CreatedAt",
                table: "AdminAuditLogs",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_AdminExternalAuthUserId_CreatedAt",
                table: "AdminAuditLogs",
                columns: new[] { "AdminExternalAuthUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_TargetUserId_CreatedAt",
                table: "AdminAuditLogs",
                columns: new[] { "TargetUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");
        }
    }
}
