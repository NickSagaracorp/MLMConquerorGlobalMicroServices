using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SeedRankSeniorityBonusCommissionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CommissionCategories",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "Description", "IsActive", "LastUpdateBy", "LastUpdateDate", "Name" },
                values: new object[] { 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Once-per-rank bonus for holding a rank ≥14 consecutive days.", true, null, null, "Rank Seniority Bonus" });

            migrationBuilder.InsertData(
                table: "CommissionTypes",
                columns: new[] { "Id", "Amount", "AmountPromo", "CommissionCategoryId", "CreatedBy", "CreationDate", "Cummulative", "CurrentRank", "DaysAfterJoining", "Description", "EnrollmentTeam", "ExternalMembers", "IsActive", "IsEnrollmentBased", "IsPaidOnRenewal", "IsPaidOnSignup", "IsRealTime", "IsSponsorBonus", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifeTimeRank", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MembersRebill", "Name", "NewMembers", "PaymentDelayDays", "Percentage", "PersonalPoints", "ResidualBased", "ResidualOverCommissionType", "ResidualPercentage", "ReverseId", "SponsoredMembers", "TeamPoints", "TriggerOrder" },
                values: new object[,]
                {
                    { 86, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Silver (rank 1). Grant when ambassador holds Silver ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 1, 0.5, 0.5, 0, "Rank Seniority Bonus – Silver", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 87, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Gold (rank 2). Grant when ambassador holds Gold ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 2, 0.5, 0.5, 0, "Rank Seniority Bonus – Gold", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 88, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Platinum (rank 3). Grant when ambassador holds Platinum ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 3, 0.5, 0.5, 0, "Rank Seniority Bonus – Platinum", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 89, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Titanium (rank 4). Grant when ambassador holds Titanium ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 4, 0.5, 0.5, 0, "Rank Seniority Bonus – Titanium", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 90, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Jade (rank 5). Grant when ambassador holds Jade ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 5, 0.5, 0.5, 0, "Rank Seniority Bonus – Jade", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 91, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Pearl (rank 6). Grant when ambassador holds Pearl ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 6, 0.5, 0.5, 0, "Rank Seniority Bonus – Pearl", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 92, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Emerald (rank 7). Grant when ambassador holds Emerald ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 7, 0.5, 0.5, 0, "Rank Seniority Bonus – Emerald", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 93, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Ruby (rank 8). Grant when ambassador holds Ruby ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 8, 0.5, 0.5, 0, "Rank Seniority Bonus – Ruby", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 94, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Sapphire (rank 9). Grant when ambassador holds Sapphire ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 9, 0.5, 0.5, 0, "Rank Seniority Bonus – Sapphire", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 95, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Diamond (rank 10). Grant when ambassador holds Diamond ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 10, 0.5, 0.5, 0, "Rank Seniority Bonus – Diamond", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 96, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Double Diamond (rank 11). Grant when ambassador holds Double Diamond ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 11, 0.5, 0.5, 0, "Rank Seniority Bonus – Double Diamond", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 97, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Triple Diamond (rank 12). Grant when ambassador holds Triple Diamond ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 12, 0.5, 0.5, 0, "Rank Seniority Bonus – Triple Diamond", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 98, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Blue Diamond (rank 13). Grant when ambassador holds Blue Diamond ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 13, 0.5, 0.5, 0, "Rank Seniority Bonus – Blue Diamond", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 99, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Black Diamond (rank 14). Grant when ambassador holds Black Diamond ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 14, 0.5, 0.5, 0, "Rank Seniority Bonus – Black Diamond", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 100, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Royal (rank 15). Grant when ambassador holds Royal ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 15, 0.5, 0.5, 0, "Rank Seniority Bonus – Royal", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 101, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Double Royal (rank 16). Grant when ambassador holds Double Royal ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 16, 0.5, 0.5, 0, "Rank Seniority Bonus – Double Royal", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 102, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Triple Royal (rank 17). Grant when ambassador holds Triple Royal ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 17, 0.5, 0.5, 0, "Rank Seniority Bonus – Triple Royal", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 103, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Blue Royal (rank 18). Grant when ambassador holds Blue Royal ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 18, 0.5, 0.5, 0, "Rank Seniority Bonus – Blue Royal", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 104, 100m, null, 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Once-per-rank seniority bonus for Black Royal (rank 19). Grant when ambassador holds Black Royal ≥14 consecutive days.", 0, 0, true, false, false, false, false, false, null, null, 0, 19, 0.5, 0.5, 0, "Rank Seniority Bonus – Black Royal", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 9);
        }
    }
}
