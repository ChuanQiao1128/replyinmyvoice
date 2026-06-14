using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditExpiryReminderClaimIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RewriteCredits_ExpiryReminderSentAt_ExpiresAt",
                table: "RewriteCredits",
                columns: new[] { "ExpiryReminderSentAt", "ExpiresAt" },
                filter: "[ExpiryReminderSentAt] IS NULL AND [ExpiresAt] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewriteCredits_ExpiryReminderSentAt_ExpiresAt",
                table: "RewriteCredits");
        }
    }
}
