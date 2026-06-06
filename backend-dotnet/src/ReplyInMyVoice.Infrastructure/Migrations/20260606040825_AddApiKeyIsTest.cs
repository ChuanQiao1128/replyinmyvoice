using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyIsTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                table: "ApiKeys",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTest",
                table: "ApiKeys");
        }
    }
}
