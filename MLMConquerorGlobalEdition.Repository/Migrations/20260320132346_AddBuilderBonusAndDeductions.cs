using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBuilderBonusAndDeductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CommissionCategories",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "Description", "IsActive", "LastUpdateBy", "LastUpdateDate", "Name" },
                values: new object[,]
                {
                    { 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Standard sponsor bonus paid on top of Member Bonus when a qualifying ambassador enrolls a new member.", true, null, null, "Builder Bonus" },
                    { 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Enhanced sponsor bonus program with elevated payout rates, completely separate from standard Builder Bonus.", true, null, null, "Builder Bonus Turbo" },
                    { 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Administrative fee deductions and token-related deductions applied at payout or on token consumption.", true, null, null, "Deductions" }
                });

            migrationBuilder.InsertData(
                table: "CommissionTypes",
                columns: new[] { "Id", "CommissionCategoryId", "CreatedBy", "CreationDate", "Cummulative", "CurrentRank", "DaysAfterJoining", "Description", "EnrollmentTeam", "ExternalMembers", "FixedAmount", "IsActive", "IsEnrollmentBased", "IsPaidOnRenewal", "IsPaidOnSignup", "IsRealTime", "IsSponsorBonus", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifeTimeRank", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MembersRebill", "Name", "NewMembers", "PaymentDelayDays", "Percentage", "PersonalPoints", "ResidualBased", "ResidualOverCommissionType", "ResidualPercentage", "ReverseId", "SponsoredMembers", "TeamPoints", "TriggerOrder" },
                values: new object[,]
                {
                    { 41, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus VIP (ID 33) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus VIP", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 42, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Elite (ID 34) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Elite", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 43, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Turbo (ID 35) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Turbo", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 44, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Turbo VIP (ID 36) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Turbo VIP", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 45, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Turbo Elite (ID 37) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Turbo Elite", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 46, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Turbo Turbo (ID 38) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Turbo Turbo", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 33, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Additional sponsor bonus paid when enrolling a VIP member. Stacks with Member Bonus.", 0, 0, 25m, true, false, false, true, true, true, null, null, 2, 0, 0.5, 0.5, 0, "Builder Bonus – VIP", 0, 4, 0m, 0, false, 0, 0.0, 41, 0, 0, 0 },
                    { 34, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Additional sponsor bonus paid when enrolling an Elite member. Stacks with Member Bonus.", 0, 0, 60m, true, false, false, true, true, true, null, null, 3, 0, 0.5, 0.5, 0, "Builder Bonus – Elite", 0, 4, 0m, 0, false, 0, 0.0, 42, 0, 0, 0 },
                    { 35, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Additional sponsor bonus paid when enrolling a Turbo member. Stacks with Member Bonus.", 0, 0, 120m, true, false, false, true, true, true, null, null, 4, 0, 0.5, 0.5, 0, "Builder Bonus – Turbo", 0, 4, 0m, 0, false, 0, 0.0, 43, 0, 0, 0 },
                    { 36, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Enhanced Builder Bonus (Turbo program) paid when enrolling a VIP member.", 0, 0, 30m, true, false, false, true, true, true, null, null, 2, 0, 0.5, 0.5, 0, "Builder Bonus Turbo – VIP", 0, 4, 0m, 0, false, 0, 0.0, 44, 0, 0, 0 },
                    { 37, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Enhanced Builder Bonus (Turbo program) paid when enrolling an Elite member.", 0, 0, 80m, true, false, false, true, true, true, null, null, 3, 0, 0.5, 0.5, 0, "Builder Bonus Turbo – Elite", 0, 4, 0m, 0, false, 0, 0.0, 45, 0, 0, 0 },
                    { 38, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Enhanced Builder Bonus (Turbo program) paid when enrolling a Turbo member.", 0, 0, 160m, true, false, false, true, true, true, null, null, 4, 0, 0.5, 0.5, 0, "Builder Bonus Turbo – Turbo", 0, 4, 0m, 0, false, 0, 0.0, 46, 0, 0, 0 },
                    { 39, 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Administrative fee deducted from gross commission payout. Default: 5% of payout total. Adjust via admin panel per comp plan version.", 0, 0, null, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "Admin Fee", 0, 0, 5m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 40, 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Deduction applied in real-time when a member consumes tokens. Unit cost can be overridden per TokenType; FixedAmount here is the platform default.", 0, 0, 1m, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Token Deduction", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
