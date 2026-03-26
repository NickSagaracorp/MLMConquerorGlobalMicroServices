using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductCommissionPromos_Products_ProductId1",
                table: "ProductCommissionPromos");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductCommissions_Products_ProductId1",
                table: "ProductCommissions");

            migrationBuilder.DropIndex(
                name: "IX_ProductCommissions_ProductId1",
                table: "ProductCommissions");

            migrationBuilder.DropIndex(
                name: "IX_ProductCommissionPromos_ProductId1",
                table: "ProductCommissionPromos");

            migrationBuilder.DropColumn(
                name: "ProductId1",
                table: "ProductCommissions");

            migrationBuilder.DropColumn(
                name: "ProductId1",
                table: "ProductCommissionPromos");

            migrationBuilder.AlterColumn<string>(
                name: "ProductId",
                table: "ProductLoyaltySettings",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PointsPerUnit",
                table: "ProductLoyaltySettings",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "ProductId",
                table: "ProductCommissions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "ProductId",
                table: "ProductCommissionPromos",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.InsertData(
                table: "ProductLoyaltySettings",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "IsActive", "LastUpdateBy", "LastUpdateDate", "PointsPerUnit", "ProductId", "RequiredSuccessfulPayments" },
                values: new object[,]
                {
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, null, null, 3m, "00000002-prod-0000-0000-000000000002", 1 },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, null, null, 6m, "00000003-prod-0000-0000-000000000003", 1 },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, null, null, 6m, "00000004-prod-0000-0000-000000000004", 1 }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "AnnualPrice", "CreatedBy", "CreationDate", "DeletedAt", "DeletedBy", "Description", "DescriptionPromo", "ImageUrl", "ImageUrlPromo", "IsActive", "IsDeleted", "LastUpdateBy", "LastUpdateDate", "MembershipLevelId", "MonthlyFee", "MonthlyFeePromo", "Name", "OldSystemProductId", "Price180Days", "Price90Days", "QualificationPoins", "QualificationPoinsPromo", "SetupFee", "SetupFeePromo" },
                values: new object[,]
                {
                    { "00000001-prod-0000-0000-000000000001", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Free guest access to the Travel Advantage platform. No qualification points. No commissions triggered. Upgrade required to earn full benefits.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, 0m, 0m, "Travel Advantage Guest Member", 1, 0m, 0m, 0, 0, 0m, 0m },
                    { "00000002-prod-0000-0000-000000000002", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Entry-level Travel Advantage membership. Earns 3 qualification points per billing cycle. Triggers VIP Member Bonus ($20) and all standard enrollment commissions.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), 2, 40m, 0m, "Travel Advantage VIP", 2, 0m, 0m, 3, 0, 0m, 0m },
                    { "00000003-prod-0000-0000-000000000003", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), 3, 99m, 0m, "Travel Advantage Elite", 3, 0m, 0m, 6, 0, 0m, 0m },
                    { "00000004-prod-0000-0000-000000000004", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Premium Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Turbo Member Bonus ($80), full commissions, and Builder Bonus Turbo program.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), 4, 199m, 0m, "Travel Advantage Turbo", 4, 0m, 0m, 6, 0, 0m, 0m },
                    { "00000005-prod-0000-0000-000000000005", 99m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Annual ambassador business fee. Operational/administrative product. Does not earn qualification points and does not trigger commissions.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), 1, 0m, 0m, "Subscription", 5, 0m, 0m, 0, 0, 99m, 0m },
                    { "00000006-prod-0000-0000-000000000006", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Generic recurring monthly subscription. Operational/administrative product. Does not earn qualification points and does not trigger commissions.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, 0m, 0m, "Monthly Subscription", 6, 0m, 0m, 0, 0, 0m, 0m }
                });

            migrationBuilder.InsertData(
                table: "ProductCommissions",
                columns: new[] { "Id", "CarBonusEligible", "CreatedBy", "CreationDate", "EligibleDailyResidual", "EligibleMembershipResidual", "LastUpdateBy", "LastUpdateDate", "PresidentialBonusEligible", "ProductId", "TriggerBoostBonus", "TriggerBuilderBonus", "TriggerBuilderBonusTurbo", "TriggerFastStartBonus", "TriggerSponsorBonus", "TriggerSponsorBonusTurbo" },
                values: new object[,]
                {
                    { 1, true, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, "00000002-prod-0000-0000-000000000002", true, true, false, true, true, false },
                    { 2, true, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, "00000003-prod-0000-0000-000000000003", true, true, false, true, true, false },
                    { 3, true, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, "00000004-prod-0000-0000-000000000004", true, true, true, true, true, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductLoyaltySettings_ProductId",
                table: "ProductLoyaltySettings",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCommissions_ProductId",
                table: "ProductCommissions",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCommissionPromos_ProductId",
                table: "ProductCommissionPromos",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCommissionPromos_Products_ProductId",
                table: "ProductCommissionPromos",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCommissions_Products_ProductId",
                table: "ProductCommissions",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductCommissionPromos_Products_ProductId",
                table: "ProductCommissionPromos");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductCommissions_Products_ProductId",
                table: "ProductCommissions");

            migrationBuilder.DropIndex(
                name: "IX_ProductLoyaltySettings_ProductId",
                table: "ProductLoyaltySettings");

            migrationBuilder.DropIndex(
                name: "IX_ProductCommissions_ProductId",
                table: "ProductCommissions");

            migrationBuilder.DropIndex(
                name: "IX_ProductCommissionPromos_ProductId",
                table: "ProductCommissionPromos");

            migrationBuilder.DeleteData(
                table: "ProductCommissions",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ProductCommissions",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ProductCommissions",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ProductLoyaltySettings",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ProductLoyaltySettings",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ProductLoyaltySettings",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: "00000001-prod-0000-0000-000000000001");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: "00000005-prod-0000-0000-000000000005");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: "00000006-prod-0000-0000-000000000006");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: "00000002-prod-0000-0000-000000000002");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: "00000003-prod-0000-0000-000000000003");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: "00000004-prod-0000-0000-000000000004");

            migrationBuilder.AlterColumn<string>(
                name: "ProductId",
                table: "ProductLoyaltySettings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<decimal>(
                name: "PointsPerUnit",
                table: "ProductLoyaltySettings",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.AlterColumn<long>(
                name: "ProductId",
                table: "ProductCommissions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "ProductId1",
                table: "ProductCommissions",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ProductId",
                table: "ProductCommissionPromos",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "ProductId1",
                table: "ProductCommissionPromos",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCommissions_ProductId1",
                table: "ProductCommissions",
                column: "ProductId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCommissionPromos_ProductId1",
                table: "ProductCommissionPromos",
                column: "ProductId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCommissionPromos_Products_ProductId1",
                table: "ProductCommissionPromos",
                column: "ProductId1",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCommissions_Products_ProductId1",
                table: "ProductCommissions",
                column: "ProductId1",
                principalTable: "Products",
                principalColumn: "Id");
        }
    }
}
