using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBuilderBonusPerRankCommissionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CommissionTypes",
                columns: new[] { "Id", "CommissionCategoryId", "CreatedBy", "CreationDate", "Cummulative", "CurrentRank", "DaysAfterJoining", "Description", "EnrollmentTeam", "ExternalMembers", "FixedAmount", "IsActive", "IsEnrollmentBased", "IsPaidOnRenewal", "IsPaidOnSignup", "IsRealTime", "IsSponsorBonus", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifeTimeRank", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MembersRebill", "Name", "NewMembers", "PaymentDelayDays", "Percentage", "PersonalPoints", "ResidualBased", "ResidualOverCommissionType", "ResidualPercentage", "ReverseId", "SponsoredMembers", "TeamPoints", "TriggerOrder" },
                values: new object[,]
                {
                    { 47, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Silver (1).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 1, 0.5, 0.5, 0, "Builder Bonus Elite – Silver", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 48, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Gold (2).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 2, 0.5, 0.5, 0, "Builder Bonus Elite – Gold", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 49, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Platinum (3).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 3, 0.5, 0.5, 0, "Builder Bonus Elite – Platinum", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 50, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Titanium (4).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 4, 0.5, 0.5, 0, "Builder Bonus Elite – Titanium", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 51, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Jade (5).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 5, 0.5, 0.5, 0, "Builder Bonus Elite – Jade", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 52, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Pearl (6).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 6, 0.5, 0.5, 0, "Builder Bonus Elite – Pearl", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 53, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Emerald (7).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 7, 0.5, 0.5, 0, "Builder Bonus Elite – Emerald", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 54, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Ruby (8).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 8, 0.5, 0.5, 0, "Builder Bonus Elite – Ruby", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 55, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Sapphire (9).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 9, 0.5, 0.5, 0, "Builder Bonus Elite – Sapphire", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 56, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Diamond (10).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 10, 0.5, 0.5, 0, "Builder Bonus Elite – Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 57, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Double Diamond (11).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 11, 0.5, 0.5, 0, "Builder Bonus Elite – Double Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 58, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Triple Diamond (12).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 12, 0.5, 0.5, 0, "Builder Bonus Elite – Triple Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 59, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Blue Diamond (13).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 13, 0.5, 0.5, 0, "Builder Bonus Elite – Blue Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 60, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Black Diamond (14).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 14, 0.5, 0.5, 0, "Builder Bonus Elite – Black Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 61, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Royal (15).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 15, 0.5, 0.5, 0, "Builder Bonus Elite – Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 62, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Double Royal (16).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 16, 0.5, 0.5, 0, "Builder Bonus Elite – Double Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 63, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Triple Royal (17).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 17, 0.5, 0.5, 0, "Builder Bonus Elite – Triple Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 64, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Blue Royal (18).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 18, 0.5, 0.5, 0, "Builder Bonus Elite – Blue Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 65, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Elite enrollment. Requires lifetime rank Black Royal (19).", 0, 0, 0m, true, false, false, true, true, true, null, null, 3, 19, 0.5, 0.5, 0, "Builder Bonus Elite – Black Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 66, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Silver (1).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 1, 0.5, 0.5, 0, "Builder Bonus Turbo – Silver", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 67, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Gold (2).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 2, 0.5, 0.5, 0, "Builder Bonus Turbo – Gold", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 68, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Platinum (3).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 3, 0.5, 0.5, 0, "Builder Bonus Turbo – Platinum", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 69, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Titanium (4).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 4, 0.5, 0.5, 0, "Builder Bonus Turbo – Titanium", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 70, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Jade (5).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 5, 0.5, 0.5, 0, "Builder Bonus Turbo – Jade", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 71, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Pearl (6).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 6, 0.5, 0.5, 0, "Builder Bonus Turbo – Pearl", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 72, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Emerald (7).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 7, 0.5, 0.5, 0, "Builder Bonus Turbo – Emerald", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 73, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Ruby (8).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 8, 0.5, 0.5, 0, "Builder Bonus Turbo – Ruby", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 74, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Sapphire (9).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 9, 0.5, 0.5, 0, "Builder Bonus Turbo – Sapphire", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 75, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Diamond (10).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 10, 0.5, 0.5, 0, "Builder Bonus Turbo – Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 76, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Double Diamond (11).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 11, 0.5, 0.5, 0, "Builder Bonus Turbo – Double Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 77, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Triple Diamond (12).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 12, 0.5, 0.5, 0, "Builder Bonus Turbo – Triple Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 78, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Blue Diamond (13).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 13, 0.5, 0.5, 0, "Builder Bonus Turbo – Blue Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 79, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Black Diamond (14).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 14, 0.5, 0.5, 0, "Builder Bonus Turbo – Black Diamond", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 80, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Royal (15).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 15, 0.5, 0.5, 0, "Builder Bonus Turbo – Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 81, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Double Royal (16).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 16, 0.5, 0.5, 0, "Builder Bonus Turbo – Double Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 82, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Triple Royal (17).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 17, 0.5, 0.5, 0, "Builder Bonus Turbo – Triple Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 83, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Blue Royal (18).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 18, 0.5, 0.5, 0, "Builder Bonus Turbo – Blue Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 84, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Builder Bonus for Turbo enrollment. Requires lifetime rank Black Royal (19).", 0, 0, 0m, true, false, false, true, true, true, null, null, 4, 19, 0.5, 0.5, 0, "Builder Bonus Turbo – Black Royal", 0, 4, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 84);
        }
    }
}
