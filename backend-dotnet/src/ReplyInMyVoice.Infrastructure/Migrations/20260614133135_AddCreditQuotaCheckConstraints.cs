using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditQuotaCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_UsagePeriods_Counts_NonNegative",
                table: "UsagePeriods",
                sql: "[UsedCount] >= 0 AND [ReservedCount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RewriteCredits_Consumed_Range",
                table: "RewriteCredits",
                sql: "[AmountConsumed] >= 0 AND [AmountConsumed] <= [AmountGranted]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UsagePeriods_Counts_NonNegative",
                table: "UsagePeriods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RewriteCredits_Consumed_Range",
                table: "RewriteCredits");
        }
    }
}
