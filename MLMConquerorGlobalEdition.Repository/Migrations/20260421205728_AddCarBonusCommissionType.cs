using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCarBonusCommissionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CommissionTypes",
                columns: new[] { "Id", "Amount", "AmountPromo", "CommissionCategoryId", "CreatedBy", "CreationDate", "Cummulative", "CurrentRank", "DaysAfterJoining", "Description", "EnrollmentTeam", "ExternalMembers", "IsActive", "IsEnrollmentBased", "IsPaidOnRenewal", "IsPaidOnSignup", "IsRealTime", "IsSponsorBonus", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifeTimeRank", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MembersRebill", "Name", "NewMembers", "PaymentDelayDays", "Percentage", "PersonalPoints", "ResidualBased", "ResidualOverCommissionType", "ResidualPercentage", "ReverseId", "SponsoredMembers", "TeamPoints", "TriggerOrder" },
                values: new object[] { 85, 500m, null, 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $500/month when maintaining personal activity (6+ pts) and 1,000+ Enrollment Team points in the calendar month.", 0, 0, true, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "Car Bonus", 0, 15, 0m, 6, false, 0, 0.0, 0, 0, 1000, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 85);
        }
    }
}
