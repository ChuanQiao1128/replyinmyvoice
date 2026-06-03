using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FreeBaselineZero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [UsagePeriods]
                SET [QuotaLimit] = 0,
                    [UpdatedAt] = SYSDATETIMEOFFSET(),
                    [RowVersion] = NEWID()
                WHERE [PeriodKey] = 'free:lifetime';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only cutover. Do not restore free quota automatically on rollback.
        }
    }
}
