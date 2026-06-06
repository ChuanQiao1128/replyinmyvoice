using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiRateLimitWindowsAndUsageRequestIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeyRateLimitWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeyRateLimitWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeyRateLimitWindows_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyUsages_RequestId",
                table: "ApiKeyUsages",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyRateLimitWindows_ApiKeyId_WindowStart",
                table: "ApiKeyRateLimitWindows",
                columns: new[] { "ApiKeyId", "WindowStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyRateLimitWindows_WindowStart",
                table: "ApiKeyRateLimitWindows",
                column: "WindowStart");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeyRateLimitWindows");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeyUsages_RequestId",
                table: "ApiKeyUsages");
        }
    }
}
