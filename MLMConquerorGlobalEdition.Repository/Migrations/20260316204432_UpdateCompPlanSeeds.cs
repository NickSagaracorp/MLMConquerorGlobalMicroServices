using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCompPlanSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FixedAmount",
                table: "CommissionTypes",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name" },
                values: new object[] { "One-time bonuses paid to the direct enroller on new member signup (Member Bonus).", "Enrollment Bonuses" });

            migrationBuilder.UpdateData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name" },
                values: new object[] { "3-window FSB paid when the enroller qualifies within each countdown window.", "Fast Start Bonus" });

            migrationBuilder.UpdateData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Fixed daily earnings based on current binary rank qualification.", "Dual Team Residuals" });

            migrationBuilder.UpdateData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Boost Bonus (weekly), Presidential Bonus (monthly), Car Bonus (monthly).", "Leadership Bonuses" });

            migrationBuilder.InsertData(
                table: "CommissionCategories",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "Description", "IsActive", "LastUpdateBy", "LastUpdateDate", "Name" },
                values: new object[] { 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Negative-amount entries that reverse previously paid commissions within the chargeback window.", true, null, null, "Reversals" });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "FixedAmount", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId" },
                values: new object[] { "One-time $20 bonus to the direct enroller when a VIP member signs up.", 20m, 2, "Member Bonus – VIP", 4, 0m, 29 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DaysAfterJoining", "Description", "FixedAmount", "IsSponsorBonus", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId", "TriggerOrder" },
                values: new object[] { 0, "One-time $40 bonus to the direct enroller when an Elite member signs up.", 40m, true, 3, "Member Bonus – Elite", 4, 0m, 30, 0 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DaysAfterJoining", "Description", "FixedAmount", "IsSponsorBonus", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId", "TriggerOrder" },
                values: new object[] { 0, "One-time $80 bonus to the direct enroller when a Turbo member signs up.", 80m, true, 4, "Member Bonus – Turbo", 4, 0m, 31, 0 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CommissionCategoryId", "DaysAfterJoining", "Description", "FixedAmount", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId", "TriggerOrder" },
                values: new object[] { 2, 14, "Earn $150 when enrolling within your first 14 days as an ambassador.", 150m, 1, "Fast Start Bonus – Window 1", 0, 0m, 32, 1 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "DaysAfterJoining", "Description", "FixedAmount", "IsPaidOnSignup", "IsRealTime", "LevelNo", "Name", "Percentage", "ResidualBased", "ReverseId", "TeamPoints", "TriggerOrder" },
                values: new object[] { 7, "Earn $150 within 7 days of triggering Reset 1 (after earning Window 1 bonus).", 150m, true, true, 1, "Fast Start Bonus – Window 2", 0m, false, 32, 0, 2 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CommissionCategoryId", "DaysAfterJoining", "Description", "FixedAmount", "IsPaidOnSignup", "IsRealTime", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId", "TeamPoints", "TriggerOrder" },
                values: new object[] { 2, 7, "Earn $150 within 7 days of triggering Reset 2 (after earning Window 2 bonus).", 150m, true, true, 1, "Fast Start Bonus – Window 3", 0, 0m, 32, 0, 3 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "FixedAmount", "Name", "PaymentDelayDays", "Percentage", "ResidualBased", "TeamPoints" },
                values: new object[] { "Earn $4/day when qualifying at Silver rank (18 Enrollment Team points).", 4m, "DTR – Silver", 4, 0m, true, 18 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Description", "FixedAmount", "Name", "PaymentDelayDays", "Percentage", "ResidualBased", "TeamPoints" },
                values: new object[] { "Earn $10/day when qualifying at Gold rank (72 Enrollment Team points).", 10m, "DTR – Gold", 4, 0m, true, 72 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Description", "FixedAmount", "Name", "PaymentDelayDays", "Percentage", "ResidualBased", "TeamPoints" },
                values: new object[] { "Earn $15/day when qualifying at Platinum rank (175 Enrollment Team points).", 15m, "DTR – Platinum", 4, 0m, true, 175 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CommissionCategoryId", "Description", "FixedAmount", "IsRealTime", "Name", "PaymentDelayDays", "ResidualBased", "TeamPoints" },
                values: new object[] { 3, "Earn $25/day when qualifying at Titanium rank (350 Dual Team points).", 25m, false, "DTR – Titanium", 4, true, 350 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CommissionCategoryId", "Description", "FixedAmount", "IsRealTime", "Name", "PaymentDelayDays", "ResidualBased", "TeamPoints" },
                values: new object[] { 3, "Earn $40/day when qualifying at Jade rank (700 Dual Team points).", 40m, false, "DTR – Jade", 4, true, 700 });

            migrationBuilder.InsertData(
                table: "CommissionTypes",
                columns: new[] { "Id", "CommissionCategoryId", "CreatedBy", "CreationDate", "Cummulative", "CurrentRank", "DaysAfterJoining", "Description", "EnrollmentTeam", "ExternalMembers", "FixedAmount", "IsActive", "IsPaidOnRenewal", "IsPaidOnSignup", "IsRealTime", "IsSponsorBonus", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifeTimeRank", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MembersRebill", "Name", "NewMembers", "PaymentDelayDays", "Percentage", "PersonalPoints", "ResidualBased", "ResidualOverCommissionType", "ResidualPercentage", "ReverseId", "SponsoredMembers", "TeamPoints", "TriggerOrder" },
                values: new object[,]
                {
                    { 12, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $80/day when qualifying at Pearl rank (1,500 Dual Team points).", 0, 0, 80m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Pearl", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 1500, 0 },
                    { 13, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $150/day when qualifying at Emerald rank (3,000 Dual Team points).", 0, 0, 150m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Emerald", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 3000, 0 },
                    { 14, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $300/day when qualifying at Ruby rank (6,000 Dual Team points).", 0, 0, 300m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Ruby", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 6000, 0 },
                    { 15, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $500/day when qualifying at Sapphire rank (10,000 Dual Team points).", 0, 0, 500m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Sapphire", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 10000, 0 },
                    { 16, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $750/day when qualifying at Diamond rank (15,000 Dual Team points).", 0, 0, 750m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 15000, 0 },
                    { 17, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $1,000/day when qualifying at Double Diamond (20,000 DT points).", 0, 0, 1000m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Double Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 20000, 0 },
                    { 18, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $1,500/day when qualifying at Triple Diamond (30,000 DT points).", 0, 0, 1500m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Triple Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 30000, 0 },
                    { 19, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $2,000/day when qualifying at Blue Diamond (60,000 DT points).", 0, 0, 2000m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Blue Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 60000, 0 },
                    { 20, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $3,000/day when qualifying at Black Diamond (120,000 DT points).", 0, 0, 3000m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Black Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 120000, 0 },
                    { 21, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $4,000/day when qualifying at Royal rank (200,000 DT points).", 0, 0, 4000m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 200000, 0 },
                    { 22, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $5,000/day when qualifying at Double Royal (300,000 DT points).", 0, 0, 5000m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Double Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 300000, 0 },
                    { 23, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $7,500/day when qualifying at Triple Royal (400,000 DT points).", 0, 0, 7500m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Triple Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 400000, 0 },
                    { 24, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $10,000/day when qualifying at Blue Royal (500,000 DT points).", 0, 0, 10000m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Blue Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 500000, 0 },
                    { 25, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $15,000/day when qualifying at Black Royal (700,000 DT points).", 0, 0, 15000m, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Black Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 700000, 0 },
                    { 26, 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $600 when 6+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Gold.", 0, 0, 600m, true, false, false, false, false, null, null, 0, 4, 0.5, 0.5, 0, "Boost Bonus – Gold", 6, 15, 0m, 0, false, 0, 0.0, 0, 0, 0, 1 },
                    { 27, 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $1,200 when 12+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Platinum. Supersedes Gold if both qualify.", 0, 0, 1200m, true, false, false, false, false, null, null, 0, 5, 0.5, 0.5, 0, "Boost Bonus – Platinum", 12, 15, 0m, 0, false, 0, 0.0, 0, 0, 0, 2 },
                    { 28, 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn 20% of your Dual Team second-leg volume monthly. Unlocked at Jade rank (Lifetime Rank). Paid on the 15th.", 0, 0, null, true, false, false, false, false, null, null, 0, 6, 0.5, 0.5, 0, "Presidential Bonus", 0, 15, 20m, 0, false, 0, 0.0, 0, 0, 0, 0 }
                });

            migrationBuilder.UpdateData(
                table: "MembershipLevels",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "IsAutoRenew", "IsFree", "Name", "Price", "RenewalPrice" },
                values: new object[] { "Annual business fee for team-building ambassadors. Qualifies for all commissions.", true, false, "Lifestyle Ambassador", 99m, 99m });

            migrationBuilder.UpdateData(
                table: "MembershipLevels",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name", "Price", "RenewalPrice" },
                values: new object[] { "Entry-level travel membership. 1 qualification point/month. Triggers $20 Member Bonus to enroller.", "Travel Advantage – VIP", 40m, 40m });

            migrationBuilder.UpdateData(
                table: "MembershipLevels",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name", "Price", "RenewalPrice" },
                values: new object[] { "Full travel membership. 6 qualification points/month. Triggers $40 Member Bonus to enroller.", "Travel Advantage – Elite", 99m, 99m });

            migrationBuilder.UpdateData(
                table: "MembershipLevels",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name", "Price", "RenewalPrice" },
                values: new object[] { "Premium travel membership. 6 qualification points/month. Triggers $80 Member Bonus to enroller.", "Travel Advantage – Turbo", 199m, 199m });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name" },
                values: new object[] { "18 ET points (3 Elite/Turbo members). DTR: $4/day.", "Silver" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name" },
                values: new object[] { "72 ET points (12 Elite/Turbo members). DTR: $10/day.", "Gold" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name" },
                values: new object[] { "175 ET points. DTR: $15/day. Boost Bonus unlocked.", "Platinum" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name" },
                values: new object[] { "350 DT / 175 ET points. DTR: $25/day.", "Titanium" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Name" },
                values: new object[] { "700 DT / 350 ET points. DTR: $40/day. Presidential unlocked.", "Jade" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Description", "Name" },
                values: new object[] { "1,500 DT / 750 ET points. DTR: $80/day.", "Pearl" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Name" },
                values: new object[] { "3,000 DT / 1,500 ET points. DTR: $150/day.", "Emerald" });

            migrationBuilder.InsertData(
                table: "RankDefinitions",
                columns: new[] { "Id", "CertificateTemplateUrl", "CreatedBy", "CreationDate", "Description", "LastUpdateBy", "LastUpdateDate", "Name", "SortOrder", "Status" },
                values: new object[,]
                {
                    { 8, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "6,000 DT / 3,000 ET points. DTR: $300/day.", null, null, "Ruby", 8, 1 },
                    { 9, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "10,000 DT / 5,000 ET points. DTR: $500/day.", null, null, "Sapphire", 9, 1 },
                    { 10, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "15,000 DT / 7,500 ET points. DTR: $750/day.", null, null, "Diamond", 10, 1 },
                    { 11, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "20,000 DT / 10,000 ET points. DTR: $1,000/day.", null, null, "Double Diamond", 11, 1 },
                    { 12, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "30,000 DT / 15,000 ET points. DTR: $1,500/day.", null, null, "Triple Diamond", 12, 1 },
                    { 13, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "60,000 DT / 30,000 ET points. DTR: $2,000/day.", null, null, "Blue Diamond", 13, 1 },
                    { 14, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "120,000 DT / 60,000 ET points. DTR: $3,000/day.", null, null, "Black Diamond", 14, 1 },
                    { 15, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "200,000 DT / 100,000 ET points. DTR: $4,000/day.", null, null, "Royal", 15, 1 },
                    { 16, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "300,000 DT / 150,000 ET points. DTR: $5,000/day.", null, null, "Double Royal", 16, 1 },
                    { 17, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "400,000 DT / 200,000 ET points. DTR: $7,500/day.", null, null, "Triple Royal", 17, 1 },
                    { 18, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "500,000 DT / 250,000 ET points. DTR: $10,000/day.", null, null, "Blue Royal", 18, 1 },
                    { 19, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "700,000 DT / 350,000 ET points. DTR: $15,000/day.", null, null, "Black Royal", 19, 1 }
                });

            migrationBuilder.InsertData(
                table: "CommissionTypes",
                columns: new[] { "Id", "CommissionCategoryId", "CreatedBy", "CreationDate", "Cummulative", "CurrentRank", "DaysAfterJoining", "Description", "EnrollmentTeam", "ExternalMembers", "FixedAmount", "IsActive", "IsPaidOnRenewal", "IsPaidOnSignup", "IsRealTime", "IsSponsorBonus", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifeTimeRank", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MembersRebill", "Name", "NewMembers", "PaymentDelayDays", "Percentage", "PersonalPoints", "ResidualBased", "ResidualOverCommissionType", "ResidualPercentage", "ReverseId", "SponsoredMembers", "TeamPoints", "TriggerOrder" },
                values: new object[,]
                {
                    { 29, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a VIP Member Bonus when the member cancels within 14 days.", 0, 0, null, true, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Member Bonus VIP", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 30, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses an Elite Member Bonus when the member cancels within 14 days.", 0, 0, null, true, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Member Bonus Elite", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 31, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Turbo Member Bonus when the member cancels within 14 days.", 0, 0, null, true, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Member Bonus Turbo", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 32, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses any FSB window earning when a signup cancels within 14 days.", 0, 0, null, true, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Fast Start Bonus", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "FixedAmount",
                table: "CommissionTypes");

            migrationBuilder.UpdateData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name" },
                values: new object[] { "One-time bonuses triggered on new member signup.", "Signup Bonuses" });

            migrationBuilder.UpdateData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Recurring commissions calculated on binary team volume.", "Residual Commissions" });

            migrationBuilder.UpdateData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Bonuses awarded for reaching leadership thresholds.", "Leadership Bonuses" });

            migrationBuilder.UpdateData(
                table: "CommissionCategories",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Negative-amount entries that reverse previously paid commissions.", "Reversals" });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId" },
                values: new object[] { "One-time bonus to the direct sponsor when a new ambassador or member signs up.", 0, "Sponsor Bonus", 14, 10m, 10 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DaysAfterJoining", "Description", "IsSponsorBonus", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId", "TriggerOrder" },
                values: new object[] { 30, "FSB for personal signups in days 1–30 after the ambassador's own enrollment.", false, 0, "Fast Start Bonus – Window 1", 7, 50m, 11, 1 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DaysAfterJoining", "Description", "IsSponsorBonus", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId", "TriggerOrder" },
                values: new object[] { 60, "FSB for personal signups in days 31–60 after the ambassador's own enrollment.", false, 0, "Fast Start Bonus – Window 2", 7, 30m, 11, 2 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CommissionCategoryId", "DaysAfterJoining", "Description", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId", "TriggerOrder" },
                values: new object[] { 1, 90, "FSB for personal signups in days 61–90 after the ambassador's own enrollment.", 0, "Fast Start Bonus – Window 3", 7, 20m, 11, 3 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "DaysAfterJoining", "Description", "IsPaidOnSignup", "IsRealTime", "LevelNo", "Name", "Percentage", "ResidualBased", "ReverseId", "TeamPoints", "TriggerOrder" },
                values: new object[] { 0, "Nightly binary team volume commission. Calculated from MemberStatisticEntity.DualTeamPoints.", false, false, 0, "Daily Residual – Binary", 10m, true, 0, 300, 0 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CommissionCategoryId", "DaysAfterJoining", "Description", "IsPaidOnSignup", "IsRealTime", "LevelNo", "Name", "PaymentDelayDays", "Percentage", "ReverseId", "TeamPoints", "TriggerOrder" },
                values: new object[] { 3, 0, "Weekly bonus for ambassadors reaching the Gold threshold.", false, false, 0, "Boost Bonus – Gold", 3, 5m, 0, 1000, 0 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Name", "PaymentDelayDays", "Percentage", "ResidualBased", "TeamPoints" },
                values: new object[] { "Weekly bonus for ambassadors reaching the Platinum threshold.", "Boost Bonus – Platinum", 3, 8m, false, 3000 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Description", "Name", "PaymentDelayDays", "Percentage", "ResidualBased", "TeamPoints" },
                values: new object[] { "Monthly bonus calculated on total organizational volume for Presidential-rank ambassadors.", "Presidential Bonus", 7, 3m, false, 0 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Description", "Name", "PaymentDelayDays", "Percentage", "ResidualBased", "TeamPoints" },
                values: new object[] { "Percentage of direct downline daily residual earnings, paid to the upline ambassador.", "Matching Bonus", 0, 20m, false, 0 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CommissionCategoryId", "Description", "IsRealTime", "Name", "PaymentDelayDays", "ResidualBased", "TeamPoints" },
                values: new object[] { 4, "Negative-amount reversal of the Sponsor Bonus when a signup cancels within 14 days.", true, "Sponsor Bonus Reversal", 0, false, 0 });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CommissionCategoryId", "Description", "IsRealTime", "Name", "PaymentDelayDays", "ResidualBased", "TeamPoints" },
                values: new object[] { 4, "Negative-amount reversal of any Fast Start Bonus window when a signup cancels.", true, "Fast Start Bonus Reversal", 0, false, 0 });

            migrationBuilder.UpdateData(
                table: "MembershipLevels",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "IsAutoRenew", "IsFree", "Name", "Price", "RenewalPrice" },
                values: new object[] { "Customer account. No team-building access. No commission eligibility.", false, true, "External Member", 0m, 0m });

            migrationBuilder.UpdateData(
                table: "MembershipLevels",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name", "Price", "RenewalPrice" },
                values: new object[] { "Entry-level ambassador. Qualifies for Sponsor Bonus and Fast Start Bonus.", "Ambassador – Basic", 99m, 79m });

            migrationBuilder.UpdateData(
                table: "MembershipLevels",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name", "Price", "RenewalPrice" },
                values: new object[] { "Mid-tier ambassador. Qualifies for Daily Residual and Boost Bonus.", "Ambassador – Advanced", 199m, 169m });

            migrationBuilder.UpdateData(
                table: "MembershipLevels",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name", "Price", "RenewalPrice" },
                values: new object[] { "Top-tier ambassador. Qualifies for all bonuses including Presidential and Matching.", "Ambassador – Premium", 399m, 349m });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Member" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Bronze" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Silver" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Gold" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Platinum" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Diamond" });

            migrationBuilder.UpdateData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Presidential" });
        }
    }
}
