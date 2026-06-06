using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRewriteAttemptApiKeyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApiKeyId",
                table: "RewriteAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewriteAttempts_ApiKeyId",
                table: "RewriteAttempts",
                column: "ApiKeyId");

            migrationBuilder.AddForeignKey(
                name: "FK_RewriteAttempts_ApiKeys_ApiKeyId",
                table: "RewriteAttempts",
                column: "ApiKeyId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RewriteAttempts_ApiKeys_ApiKeyId",
                table: "RewriteAttempts");

            migrationBuilder.DropIndex(
                name: "IX_RewriteAttempts_ApiKeyId",
                table: "RewriteAttempts");

            migrationBuilder.DropColumn(
                name: "ApiKeyId",
                table: "RewriteAttempts");
        }
    }
}
