using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrichRewriteCreditGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewriteCredits_StripeEventId",
                table: "RewriteCredits");

            migrationBuilder.AddColumn<long>(
                name: "StripeAmountTotal",
                table: "RewriteCredits",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCurrency",
                table: "RewriteCredits",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentIntentId",
                table: "RewriteCredits",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSku",
                table: "RewriteCredits",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCredits_StripeEventId",
                table: "RewriteCredits",
                column: "StripeEventId",
                unique: true,
                filter: "[StripeEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCredits_StripePaymentIntentId",
                table: "RewriteCredits",
                column: "StripePaymentIntentId",
                filter: "[StripePaymentIntentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewriteCredits_StripeEventId",
                table: "RewriteCredits");

            migrationBuilder.DropIndex(
                name: "IX_RewriteCredits_StripePaymentIntentId",
                table: "RewriteCredits");

            migrationBuilder.DropColumn(
                name: "StripeAmountTotal",
                table: "RewriteCredits");

            migrationBuilder.DropColumn(
                name: "StripeCurrency",
                table: "RewriteCredits");

            migrationBuilder.DropColumn(
                name: "StripePaymentIntentId",
                table: "RewriteCredits");

            migrationBuilder.DropColumn(
                name: "StripeSku",
                table: "RewriteCredits");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCredits_StripeEventId",
                table: "RewriteCredits",
                column: "StripeEventId");
        }
    }
}
