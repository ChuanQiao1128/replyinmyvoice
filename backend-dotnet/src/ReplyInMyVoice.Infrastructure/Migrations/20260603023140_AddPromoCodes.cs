using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPromoCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromoCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DisplayCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreditsGranted = table.Column<int>(type: "int", nullable: false),
                    GrantTtlDays = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MaxRedemptionsGlobal = table.Column<int>(type: "int", nullable: true),
                    MaxRedemptionsPerUser = table.Column<int>(type: "int", nullable: false),
                    RedemptionCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodes", x => x.Id);
                    table.CheckConstraint("CK_PromoCodes_CreditsGranted_Positive", "[CreditsGranted] > 0");
                    table.CheckConstraint("CK_PromoCodes_GrantTtlDays_Positive", "[GrantTtlDays] > 0");
                    table.CheckConstraint("CK_PromoCodes_MaxRedemptionsGlobal_PositiveOrNull", "[MaxRedemptionsGlobal] IS NULL OR [MaxRedemptionsGlobal] > 0");
                    table.CheckConstraint("CK_PromoCodes_MaxRedemptionsPerUser_Minimum", "[MaxRedemptionsPerUser] >= 1");
                    table.CheckConstraint("CK_PromoCodes_RedemptionCount_NonNegative", "[RedemptionCount] >= 0");
                    table.CheckConstraint("CK_PromoCodes_ValidUntil_After_ValidFrom", "[ValidUntil] > [ValidFrom]");
                });

            migrationBuilder.CreateTable(
                name: "PromoCodeRedemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromoCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RewriteCreditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditsGranted = table.Column<int>(type: "int", nullable: false),
                    CodeSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RedeemIpHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReversedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodeRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromoCodeRedemptions_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromoCodeRedemptions_PromoCodes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalTable: "PromoCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeRedemptions_PromoCodeId",
                table: "PromoCodeRedemptions",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeRedemptions_PromoCodeId_UserId",
                table: "PromoCodeRedemptions",
                columns: new[] { "PromoCodeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeRedemptions_RedeemedAt",
                table: "PromoCodeRedemptions",
                column: "RedeemedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeRedemptions_RedeemIpHash",
                table: "PromoCodeRedemptions",
                column: "RedeemIpHash");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeRedemptions_RewriteCreditId",
                table: "PromoCodeRedemptions",
                column: "RewriteCreditId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeRedemptions_UserId",
                table: "PromoCodeRedemptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_Code",
                table: "PromoCodes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromoCodeRedemptions");

            migrationBuilder.DropTable(
                name: "PromoCodes");
        }
    }
}
