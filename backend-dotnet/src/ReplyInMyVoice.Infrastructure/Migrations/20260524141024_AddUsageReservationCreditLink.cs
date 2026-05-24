using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageReservationCreditLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RewriteCreditId",
                table: "UsageReservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageReservations_RewriteCreditId",
                table: "UsageReservations",
                column: "RewriteCreditId");

            migrationBuilder.AddForeignKey(
                name: "FK_UsageReservations_RewriteCredits_RewriteCreditId",
                table: "UsageReservations",
                column: "RewriteCreditId",
                principalTable: "RewriteCredits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UsageReservations_RewriteCredits_RewriteCreditId",
                table: "UsageReservations");

            migrationBuilder.DropIndex(
                name: "IX_UsageReservations_RewriteCreditId",
                table: "UsageReservations");

            migrationBuilder.DropColumn(
                name: "RewriteCreditId",
                table: "UsageReservations");
        }
    }
}
