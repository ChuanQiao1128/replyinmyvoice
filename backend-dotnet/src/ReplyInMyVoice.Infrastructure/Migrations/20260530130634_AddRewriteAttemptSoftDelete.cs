using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRewriteAttemptSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "RewriteAttempts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewriteAttempts_UserId_DeletedAt_CreatedAt",
                table: "RewriteAttempts",
                columns: new[] { "UserId", "DeletedAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewriteAttempts_UserId_DeletedAt_CreatedAt",
                table: "RewriteAttempts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "RewriteAttempts");
        }
    }
}
