using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddRankRequirementsAndTokenTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TemplateUrl",
                table: "TokenTypes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "TokenTypes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TokenTypes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "TokenTypeCommissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenTypeId = table.Column<int>(type: "int", nullable: false),
                    CommissionTypeId = table.Column<int>(type: "int", nullable: false),
                    CommissionPerToken = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TriggerSponsorBonus = table.Column<bool>(type: "bit", nullable: false),
                    TriggerBuilderBonus = table.Column<bool>(type: "bit", nullable: false),
                    TriggerSponsorBonusTurbo = table.Column<bool>(type: "bit", nullable: false),
                    TriggerBuilderBonusTurbo = table.Column<bool>(type: "bit", nullable: false),
                    TriggerFastStartBonus = table.Column<bool>(type: "bit", nullable: false),
                    TriggerBoostBonus = table.Column<bool>(type: "bit", nullable: false),
                    CarBonusEligible = table.Column<bool>(type: "bit", nullable: false),
                    PresidentialBonusEligible = table.Column<bool>(type: "bit", nullable: false),
                    EligibleMembershipResidual = table.Column<bool>(type: "bit", nullable: false),
                    EligibleDailyResidual = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenTypeCommissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenTypeProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenTypeId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    QuantityGranted = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenTypeProducts", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RankRequirements",
                columns: new[] { "Id", "AchievementMessage", "CertificateUrl", "CreatedBy", "CreationDate", "CurrentRankDescription", "DailyBonus", "EnrollmentQualifiedTeamMembers", "EnrollmentTeam", "ExternalMembers", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifetimeHoldingDuration", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MonthlyBonus", "PersonalPoints", "PlacementQualifiedTeamMembers", "RankBonus", "RankDefinitionId", "RankDescription", "SalesVolume", "SponsoredMembers", "TeamPoints" },
                values: new object[,]
                {
                    { 1, "Congratulations! You have reached Silver rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Silver Ambassador. Earn $4/day in Dual Team Residuals.", 4m, 0, 3, 1, null, null, 1, 0, 0.5, 0.5, 0m, 1, 0, 100m, 1, "Qualify with 18 Enrollment Team points (3 Elite/Turbo members, max 50% per branch).", 0m, 1, 18 },
                    { 2, "Congratulations! You have reached Gold rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Gold Ambassador. Earn $10/day in Dual Team Residuals.", 10m, 0, 12, 1, null, null, 2, 0, 0.5, 0.5, 0m, 1, 0, 300m, 2, "Qualify with 72 Enrollment Team points (12 Elite/Turbo members, max 50% per branch).", 0m, 1, 72 },
                    { 3, "Congratulations! You have reached Platinum rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Platinum Ambassador. Earn $15/day in Dual Team Residuals.", 15m, 0, 29, 1, null, null, 3, 0, 0.5, 0.5, 0m, 1, 0, 500m, 3, "Qualify with 175 Enrollment Team points (max 50% per branch). Boost Bonus unlocked.", 0m, 2, 175 },
                    { 4, "Congratulations! You have reached Titanium rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Titanium Ambassador. Earn $25/day in Dual Team Residuals.", 25m, 0, 0, 1, null, null, 4, 0, 0.5, 0.5, 0m, 1, 0, 1000m, 4, "Qualify with 350 Dual Team points (max 50% per branch).", 0m, 2, 350 },
                    { 5, "Congratulations! You have reached Jade rank and unlocked the Presidential Bonus!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Jade Ambassador. Earn $40/day in Dual Team Residuals.", 40m, 0, 0, 1, null, null, 5, 0, 0.5, 0.5, 0m, 1, 0, 2500m, 5, "Qualify with 700 Dual Team points (max 50% per branch). Presidential Bonus unlocked.", 0m, 3, 700 },
                    { 6, "Congratulations! You have reached Pearl rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Pearl Ambassador. Earn $80/day in Dual Team Residuals.", 80m, 0, 0, 1, null, null, 6, 0, 0.5, 0.5, 0m, 1, 0, 5000m, 6, "Qualify with 1,500 Dual Team points (max 50% per branch).", 0m, 3, 1500 },
                    { 7, "Congratulations! You have reached Emerald rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are an Emerald Ambassador. Earn $150/day in Dual Team Residuals.", 150m, 0, 0, 1, null, null, 7, 0, 0.5, 0.5, 0m, 1, 0, 10000m, 7, "Qualify with 3,000 Dual Team points (max 50% per branch).", 0m, 4, 3000 },
                    { 8, "Congratulations! You have reached Ruby rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Ruby Ambassador. Earn $300/day in Dual Team Residuals.", 300m, 0, 0, 1, null, null, 8, 0, 0.5, 0.5, 0m, 1, 0, 25000m, 8, "Qualify with 6,000 Dual Team points (max 50% per branch).", 0m, 5, 6000 },
                    { 9, "Congratulations! You have reached Sapphire rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Sapphire Ambassador. Earn $500/day in Dual Team Residuals.", 500m, 0, 0, 1, null, null, 9, 0, 0.5, 0.5, 0m, 1, 0, 50000m, 9, "Qualify with 10,000 Dual Team points (max 50% per branch).", 0m, 5, 10000 },
                    { 10, "Congratulations! You have reached Diamond rank and unlocked the Car Bonus!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Diamond Ambassador. Earn $750/day in Dual Team Residuals.", 750m, 0, 0, 1, null, null, 10, 0, 0.5, 0.5, 500m, 1, 0, 100000m, 10, "Qualify with 15,000 Dual Team points (max 50% per branch). Car Bonus unlocked.", 0m, 6, 15000 },
                    { 11, "Congratulations! You have reached Double Diamond rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Double Diamond Ambassador. Earn $1,000/day in Dual Team Residuals.", 1000m, 0, 0, 1, null, null, 11, 0, 0.5, 0.5, 750m, 1, 0, 150000m, 11, "Qualify with 20,000 Dual Team points (max 50% per branch).", 0m, 6, 20000 },
                    { 12, "Congratulations! You have reached Triple Diamond rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Triple Diamond Ambassador. Earn $1,500/day in Dual Team Residuals.", 1500m, 0, 0, 1, null, null, 12, 0, 0.5, 0.5, 1000m, 1, 0, 200000m, 12, "Qualify with 30,000 Dual Team points (max 50% per branch).", 0m, 7, 30000 },
                    { 13, "Congratulations! You have reached Blue Diamond rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Blue Diamond Ambassador. Earn $2,000/day in Dual Team Residuals.", 2000m, 0, 0, 1, null, null, 13, 0, 0.5, 0.5, 1500m, 1, 0, 300000m, 13, "Qualify with 60,000 Dual Team points (max 50% per branch).", 0m, 8, 60000 },
                    { 14, "Congratulations! You have reached Black Diamond rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Black Diamond Ambassador. Earn $3,000/day in Dual Team Residuals.", 3000m, 0, 0, 1, null, null, 14, 0, 0.5, 0.5, 2500m, 1, 0, 500000m, 14, "Qualify with 120,000 Dual Team points (max 50% per branch).", 0m, 10, 120000 },
                    { 15, "Congratulations! You have reached Royal rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Royal Ambassador. Earn $4,000/day in Dual Team Residuals.", 4000m, 0, 0, 1, null, null, 15, 0, 0.5, 0.5, 4000m, 1, 0, 750000m, 15, "Qualify with 200,000 Dual Team points (max 50% per branch).", 0m, 12, 200000 },
                    { 16, "Congratulations! You have reached Double Royal rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Double Royal Ambassador. Earn $5,000/day in Dual Team Residuals.", 5000m, 0, 0, 1, null, null, 16, 0, 0.5, 0.5, 5000m, 1, 0, 1000000m, 16, "Qualify with 300,000 Dual Team points (max 50% per branch).", 0m, 15, 300000 },
                    { 17, "Congratulations! You have reached Triple Royal rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Triple Royal Ambassador. Earn $7,500/day in Dual Team Residuals.", 7500m, 0, 0, 1, null, null, 17, 0, 0.5, 0.5, 7500m, 1, 0, 1500000m, 17, "Qualify with 400,000 Dual Team points (max 50% per branch).", 0m, 20, 400000 },
                    { 18, "Congratulations! You have reached Blue Royal rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Blue Royal Ambassador. Earn $10,000/day in Dual Team Residuals.", 10000m, 0, 0, 1, null, null, 18, 0, 0.5, 0.5, 10000m, 1, 0, 2000000m, 18, "Qualify with 500,000 Dual Team points (max 50% per branch).", 0m, 25, 500000 },
                    { 19, "Congratulations! You have reached Black Royal — the highest rank in the company!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Black Royal Ambassador. Earn $15,000/day in Dual Team Residuals.", 15000m, 0, 0, 1, null, null, 19, 0, 0.5, 0.5, 15000m, 1, 0, 3000000m, 19, "Qualify with 700,000 Dual Team points (max 50% per branch). The pinnacle of the Ambassador journey.", 0m, 30, 700000 }
                });

            migrationBuilder.InsertData(
                table: "TokenTypeCommissions",
                columns: new[] { "Id", "CarBonusEligible", "CommissionPerToken", "CommissionTypeId", "CreatedBy", "CreationDate", "EligibleDailyResidual", "EligibleMembershipResidual", "LastUpdateBy", "LastUpdateDate", "PresidentialBonusEligible", "TokenTypeId", "TriggerBoostBonus", "TriggerBuilderBonus", "TriggerBuilderBonusTurbo", "TriggerFastStartBonus", "TriggerSponsorBonus", "TriggerSponsorBonusTurbo" },
                values: new object[,]
                {
                    { 1, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 1, true, true, false, true, true, false },
                    { 2, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 2, true, true, false, true, true, false },
                    { 3, true, 0m, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 3, true, true, true, true, true, true },
                    { 4, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 4, false, false, false, false, false, false }
                });

            migrationBuilder.InsertData(
                table: "TokenTypes",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "Description", "IsActive", "IsGuestPass", "LastUpdateBy", "LastUpdateDate", "Name", "TemplateUrl" },
                values: new object[,]
                {
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Guest pass granting access to sign up as a VIP member. Triggers VIP Member Bonus and Builder Bonus commissions for the issuing ambassador.", true, true, null, null, "VIP Guest Pass", null },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Guest pass granting access to sign up as an Elite member. Triggers Elite Member Bonus and Builder Bonus commissions for the issuing ambassador.", true, true, null, null, "Elite Guest Pass", null },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Guest pass granting access to sign up as a Turbo member. Triggers Turbo Member Bonus and Builder Bonus commissions for the issuing ambassador.", true, true, null, null, "Turbo Guest Pass", null },
                    { 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Internal credit token used to offset membership fees or purchase benefits. Does not trigger enrollment commissions.", true, false, null, null, "Ambassador Credit", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokenTypeCommissions_TokenTypeId_CommissionTypeId",
                table: "TokenTypeCommissions",
                columns: new[] { "TokenTypeId", "CommissionTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenTypeProducts_TokenTypeId_ProductId",
                table: "TokenTypeProducts",
                columns: new[] { "TokenTypeId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokenTypeCommissions");

            migrationBuilder.DropTable(
                name: "TokenTypeProducts");

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.AlterColumn<string>(
                name: "TemplateUrl",
                table: "TokenTypes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "TokenTypes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TokenTypes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
