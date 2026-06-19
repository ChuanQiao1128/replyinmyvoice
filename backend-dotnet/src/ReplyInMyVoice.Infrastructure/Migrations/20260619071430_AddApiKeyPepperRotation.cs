using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyPepperRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PepperVersion",
                table: "ApiKeys",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "RehashPending",
                table: "ApiKeys",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_RehashPending",
                table: "ApiKeys",
                column: "RehashPending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_RehashPending",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "PepperVersion",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "RehashPending",
                table: "ApiKeys");
        }
    }
}
