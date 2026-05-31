using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRewriteCreditOriginalAmountGranted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalAmountGranted",
                table: "RewriteCredits",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE RewriteCredits
                SET OriginalAmountGranted = AmountGranted
                WHERE OriginalAmountGranted IS NULL
                    AND AmountGranted > 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalAmountGranted",
                table: "RewriteCredits");
        }
    }
}
