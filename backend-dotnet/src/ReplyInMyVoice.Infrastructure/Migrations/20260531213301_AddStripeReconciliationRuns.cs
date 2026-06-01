using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeReconciliationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StripeReconciliationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WindowEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StripePaymentCount = table.Column<int>(type: "int", nullable: false),
                    PurchaseGrantCount = table.Column<int>(type: "int", nullable: false),
                    PaidButNoGrantCount = table.Column<int>(type: "int", nullable: false),
                    GrantButNoPaymentCount = table.Column<int>(type: "int", nullable: false),
                    AmountMismatchCount = table.Column<int>(type: "int", nullable: false),
                    ReportJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeReconciliationRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StripeReconciliationRuns_CompletedAt",
                table: "StripeReconciliationRuns",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StripeReconciliationRuns_WindowStart_WindowEnd",
                table: "StripeReconciliationRuns",
                columns: new[] { "WindowStart", "WindowEnd" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StripeReconciliationRuns");
        }
    }
}
