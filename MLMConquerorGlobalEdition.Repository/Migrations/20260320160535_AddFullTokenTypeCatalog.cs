using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTokenTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CommissionTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CarBonusEligible", "CommissionTypeId", "PresidentialBonusEligible", "TriggerBoostBonus", "TriggerBuilderBonus", "TriggerBuilderBonusTurbo", "TriggerFastStartBonus", "TriggerSponsorBonus", "TriggerSponsorBonusTurbo" },
                values: new object[] { false, 40, false, false, false, false, false, false, false });

            migrationBuilder.UpdateData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "EligibleDailyResidual", "EligibleMembershipResidual" },
                values: new object[] { true, true });

            migrationBuilder.InsertData(
                table: "TokenTypeCommissions",
                columns: new[] { "Id", "CarBonusEligible", "CommissionPerToken", "CommissionTypeId", "CreatedBy", "CreationDate", "EligibleDailyResidual", "EligibleMembershipResidual", "LastUpdateBy", "LastUpdateDate", "PresidentialBonusEligible", "TokenTypeId", "TriggerBoostBonus", "TriggerBuilderBonus", "TriggerBuilderBonusTurbo", "TriggerFastStartBonus", "TriggerSponsorBonus", "TriggerSponsorBonusTurbo" },
                values: new object[,]
                {
                    { 5, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 5, true, true, false, true, true, false },
                    { 6, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 6, false, false, false, false, false, false },
                    { 7, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 7, false, false, false, false, false, false },
                    { 8, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 8, false, false, false, false, false, false },
                    { 9, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 9, false, false, false, false, false, false },
                    { 10, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 10, false, false, false, false, false, false },
                    { 11, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 11, true, true, false, true, true, false },
                    { 12, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 12, true, true, false, true, true, false },
                    { 13, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 13, true, true, false, true, true, false },
                    { 14, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 14, false, false, false, false, false, false },
                    { 15, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 15, true, true, false, true, true, false },
                    { 16, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 16, true, true, false, true, true, false },
                    { 17, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 17, false, false, false, false, false, false },
                    { 19, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 19, true, true, false, true, true, false },
                    { 20, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 20, false, false, false, false, false, false },
                    { 21, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 21, false, false, false, false, false, false },
                    { 22, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 22, false, false, false, false, false, false },
                    { 23, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 23, false, false, false, false, false, false },
                    { 24, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 24, false, false, false, false, false, false },
                    { 25, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 25, false, false, false, false, false, false },
                    { 26, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 26, true, true, false, true, true, false },
                    { 27, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 27, true, true, false, true, true, false },
                    { 28, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 28, true, true, false, true, true, false },
                    { 29, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 29, true, true, false, true, true, false },
                    { 30, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 30, true, true, false, true, true, false },
                    { 31, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 31, true, true, false, true, true, false },
                    { 32, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 32, true, true, false, true, true, false },
                    { 33, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 33, true, true, false, true, true, false },
                    { 34, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 34, true, true, false, true, true, false },
                    { 35, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 35, false, false, false, false, false, false },
                    { 36, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 36, false, false, false, false, false, false },
                    { 37, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 37, false, false, false, false, false, false },
                    { 38, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 38, false, false, false, false, false, false },
                    { 39, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 39, false, false, false, false, false, false },
                    { 40, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 40, false, false, false, false, false, false },
                    { 41, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 41, false, false, false, false, false, false },
                    { 42, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 42, false, false, false, false, false, false },
                    { 43, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 43, false, false, false, false, false, false },
                    { 44, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 44, false, false, false, false, false, false },
                    { 45, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 45, false, false, false, false, false, false },
                    { 46, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 46, false, false, false, false, false, false },
                    { 47, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 47, false, false, false, false, false, false },
                    { 48, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 48, false, false, false, false, false, false },
                    { 49, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 49, true, true, false, true, true, false },
                    { 50, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 50, true, true, false, true, true, false },
                    { 51, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 51, false, false, false, false, false, false },
                    { 52, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 52, false, false, false, false, false, false },
                    { 53, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 53, false, false, false, false, false, false },
                    { 54, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 54, true, true, false, true, true, false },
                    { 55, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 55, false, false, false, false, false, false },
                    { 56, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 56, true, true, false, false, true, false },
                    { 57, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 57, true, true, false, false, true, false },
                    { 58, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 58, true, true, false, false, true, false },
                    { 59, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 59, true, true, false, false, true, false },
                    { 60, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 60, true, true, false, false, true, false },
                    { 61, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 61, false, false, false, false, false, false },
                    { 62, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 62, false, false, false, false, false, false },
                    { 63, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 63, false, false, false, false, false, false },
                    { 64, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 64, true, true, false, true, true, false },
                    { 65, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 65, true, true, false, true, true, false },
                    { 66, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 66, false, false, false, false, false, false },
                    { 67, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 67, true, true, false, true, true, false },
                    { 68, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 68, true, true, false, true, true, false },
                    { 69, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 69, true, true, true, true, true, true },
                    { 70, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 70, true, true, true, true, true, true },
                    { 71, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 71, true, true, false, true, true, false },
                    { 72, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 72, true, true, false, true, true, false },
                    { 73, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 73, true, true, true, true, true, true },
                    { 74, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 74, true, true, true, true, true, true },
                    { 75, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 75, true, true, false, true, true, false },
                    { 76, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 76, true, true, false, true, true, false },
                    { 77, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 77, true, true, false, false, true, false },
                    { 78, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 78, false, false, false, false, false, false },
                    { 79, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 79, true, true, false, true, true, false },
                    { 80, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 80, true, true, true, true, true, true },
                    { 81, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 81, false, false, false, false, false, false },
                    { 82, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 82, false, false, false, false, false, false },
                    { 83, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 83, false, false, false, false, false, false },
                    { 84, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 84, false, false, false, false, false, false },
                    { 85, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 85, false, false, false, false, false, false },
                    { 86, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 86, false, false, false, false, false, false },
                    { 87, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 87, false, false, false, false, false, false },
                    { 88, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 88, false, false, false, false, false, false },
                    { 89, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 89, false, false, false, false, false, false },
                    { 90, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 90, false, false, false, false, false, false },
                    { 91, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 91, false, false, false, false, false, false },
                    { 92, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 92, false, false, false, false, false, false },
                    { 93, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 93, false, false, false, false, false, false },
                    { 94, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 94, true, true, false, true, true, false },
                    { 95, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 95, true, true, false, true, true, false },
                    { 96, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 96, true, true, true, true, true, true },
                    { 97, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 97, true, true, false, true, true, false },
                    { 98, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 98, true, true, false, true, true, false },
                    { 99, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 99, true, true, true, true, true, true }
                });

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "IsGuestPass", "Name" },
                values: new object[] { null, false, "Enrollment: Ambassador + Pro" });

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Guest Member" });

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "IsGuestPass", "Name" },
                values: new object[] { null, false, "Monthly: Elite" });

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Monthly: VIP" });

            migrationBuilder.InsertData(
                table: "TokenTypes",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "Description", "IsActive", "IsGuestPass", "LastUpdateBy", "LastUpdateDate", "Name", "TemplateUrl" },
                values: new object[,]
                {
                    { 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite", null },
                    { 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Travel Advantage Elite (Signup)", null },
                    { 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Travel Advantage Lite", null },
                    { 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador", null },
                    { 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment Pro ($99.97 cost / no commission)", null },
                    { 10, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Annual Fee", null },
                    { 11, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + Event", null },
                    { 12, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Pro + Event", null },
                    { 13, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: VIP Member", null },
                    { 14, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Mobile App", null },
                    { 15, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Pro Member", null },
                    { 16, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Member", null },
                    { 17, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro to Elite", null },
                    { 19, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Special", null },
                    { 20, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, false, false, null, null, "--Available--", null },
                    { 21, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Annual: VIP 365", null },
                    { 22, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Pro", null },
                    { 23, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Annual: Biz Center", null },
                    { 24, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Legacy Biz Center Fee", null },
                    { 25, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Mall", null },
                    { 26, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite180", null },
                    { 27, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite180 + Event", null },
                    { 28, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Pro180", null },
                    { 29, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Pro180 + Event", null },
                    { 30, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus180", null },
                    { 31, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus180 + Event", null },
                    { 32, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite180 Member", null },
                    { 33, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Pro180 Member", null },
                    { 34, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Plus180 Member", null },
                    { 35, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus180 to Pro", null },
                    { 36, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus180 to Pro180", null },
                    { 37, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus180 to Elite", null },
                    { 38, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus180 to Elite180", null },
                    { 39, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro to Pro180", null },
                    { 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro to Elite180", null },
                    { 41, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro180 to Elite", null },
                    { 42, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro180 to Elite180", null },
                    { 43, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Elite to Elite180", null },
                    { 44, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Elite180 Level 2 (79.97)", null },
                    { 45, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Elite180 Level 3 (39.97)", null },
                    { 46, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Pro180 Level 2", null },
                    { 47, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Pro180 Level 3", null },
                    { 48, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Plus180", null },
                    { 49, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus", null },
                    { 50, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus + Event", null },
                    { 51, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Plus", null },
                    { 52, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus to Elite", null },
                    { 53, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus to Elite180", null },
                    { 54, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Plus Member", null },
                    { 55, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Elite180 (59.97)", null },
                    { 56, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to VIP", null },
                    { 57, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to VIP 365", null },
                    { 58, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to Plus", null },
                    { 59, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to Elite", null },
                    { 60, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to Elite180", null },
                    { 61, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: VIP to Plus", null },
                    { 62, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: VIP to Elite", null },
                    { 63, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: VIP to Elite180", null },
                    { 64, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP", null },
                    { 65, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP + Event", null },
                    { 66, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Elite to Turbo", null },
                    { 67, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 365", null },
                    { 68, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 365 + Event", null },
                    { 69, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO", null },
                    { 70, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + Event + TURBO", null },
                    { 71, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite (Coupon)", null },
                    { 72, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + Event (Coupon)", null },
                    { 73, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + Event + TURBO (Coupon)", null },
                    { 74, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO (Coupon)", null },
                    { 75, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 180", null },
                    { 76, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 180 + Event", null },
                    { 77, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to VIP 180", null },
                    { 78, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Recurring: VIP 180", null },
                    { 79, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: VIP 180 Member", null },
                    { 80, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Member + TURBO", null },
                    { 81, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite FREE", null },
                    { 82, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite (Coupon) FREE", null },
                    { 83, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO FREE", null },
                    { 84, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO (Coupon) FREE", null },
                    { 85, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus FREE", null },
                    { 86, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP FREE", null },
                    { 87, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 180 FREE", null },
                    { 88, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador FREE", null },
                    { 89, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Member FREE", null },
                    { 90, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Member + TURBO FREE", null },
                    { 91, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Plus Member FREE", null },
                    { 92, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: VIP Member FREE", null },
                    { 93, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: VIP 180 FREE", null },
                    { 94, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Elite Ambassador SpecialPromo", null },
                    { 95, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Plus Ambassador SpecialPromo", null },
                    { 96, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Turbo Ambassador SpecialPromo", null },
                    { 97, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus (Help a Friend)", null },
                    { 98, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite (Help a Friend)", null },
                    { 99, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO (Help a Friend)", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.UpdateData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CommissionTypeId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CarBonusEligible", "CommissionTypeId", "PresidentialBonusEligible", "TriggerBoostBonus", "TriggerBuilderBonus", "TriggerBuilderBonusTurbo", "TriggerFastStartBonus", "TriggerSponsorBonus", "TriggerSponsorBonusTurbo" },
                values: new object[] { true, 3, true, true, true, true, true, true, true });

            migrationBuilder.UpdateData(
                table: "TokenTypeCommissions",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "EligibleDailyResidual", "EligibleMembershipResidual" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "IsGuestPass", "Name" },
                values: new object[] { "Guest pass granting access to sign up as a VIP member. Triggers VIP Member Bonus and Builder Bonus commissions for the issuing ambassador.", true, "VIP Guest Pass" });

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Guest pass granting access to sign up as an Elite member. Triggers Elite Member Bonus and Builder Bonus commissions for the issuing ambassador.", "Elite Guest Pass" });

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "IsGuestPass", "Name" },
                values: new object[] { "Guest pass granting access to sign up as a Turbo member. Triggers Turbo Member Bonus and Builder Bonus commissions for the issuing ambassador.", true, "Turbo Guest Pass" });

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Internal credit token used to offset membership fees or purchase benefits. Does not trigger enrollment commissions.", "Ambassador Credit" });
        }
    }
}
