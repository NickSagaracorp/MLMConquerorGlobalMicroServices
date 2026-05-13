using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPromoBonusFlagsAndFsbHistoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DoubleBuilderBonus",
                table: "CorporatePromos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DoubleSponsorBonus",
                table: "CorporatePromos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ResetFsbCountdown",
                table: "CorporatePromos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetFsbCountdownExecutedAt",
                table: "CorporatePromos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FastStartBonus1ExtendedEnd",
                table: "CommissionCountDownHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FastStartBonus1ExtendedStart",
                table: "CommissionCountDownHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CommissionCountDownHistories",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoubleBuilderBonus",
                table: "CorporatePromos");

            migrationBuilder.DropColumn(
                name: "DoubleSponsorBonus",
                table: "CorporatePromos");

            migrationBuilder.DropColumn(
                name: "ResetFsbCountdown",
                table: "CorporatePromos");

            migrationBuilder.DropColumn(
                name: "ResetFsbCountdownExecutedAt",
                table: "CorporatePromos");

            migrationBuilder.DropColumn(
                name: "FastStartBonus1ExtendedEnd",
                table: "CommissionCountDownHistories");

            migrationBuilder.DropColumn(
                name: "FastStartBonus1ExtendedStart",
                table: "CommissionCountDownHistories");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CommissionCountDownHistories");
        }
    }
}
