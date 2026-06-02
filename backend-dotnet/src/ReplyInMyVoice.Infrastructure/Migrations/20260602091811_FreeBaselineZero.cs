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
            migrationBuilder.Sql(
                """
                UPDATE [UsagePeriods]
                SET [QuotaLimit] = 0,
                    [UpdatedAt] = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
                    [RowVersion] = NEWID()
                WHERE [PeriodKey] = 'free:lifetime';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
