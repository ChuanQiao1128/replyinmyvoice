using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    // MIGRATION-RISK-ACCEPTED: Issue #864 removes the inert ApiKeys.Scope column because no handler exposes, populates, or enforces it.
    /// <inheritdoc />
    public partial class RemoveApiKeyScopeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Scope",
                table: "ApiKeys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "ApiKeys",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
