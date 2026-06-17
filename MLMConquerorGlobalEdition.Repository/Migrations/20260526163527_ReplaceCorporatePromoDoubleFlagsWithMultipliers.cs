using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCorporatePromoDoubleFlagsWithMultipliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: add the new int columns with default 1 (= no boost).
            migrationBuilder.AddColumn<int>(
                name: "SponsorBonusMultiplier",
                table: "CorporatePromos",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BuilderBonusMultiplier",
                table: "CorporatePromos",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Step 2: backfill from the old bool columns BEFORE dropping them.
            // true (2× promo) → 2, false (no boost) → 1.
            migrationBuilder.Sql(@"
                UPDATE CorporatePromos
                SET SponsorBonusMultiplier = CASE WHEN DoubleSponsorBonus = 1 THEN 2 ELSE 1 END,
                    BuilderBonusMultiplier = CASE WHEN DoubleBuilderBonus = 1 THEN 2 ELSE 1 END;
            ");

            // Step 3: drop the old bool columns.
            migrationBuilder.DropColumn(
                name: "DoubleSponsorBonus",
                table: "CorporatePromos");

            migrationBuilder.DropColumn(
                name: "DoubleBuilderBonus",
                table: "CorporatePromos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: re-add the bool columns, backfill from multipliers (>=2 → true),
            // drop the multiplier columns. Multipliers above 2 collapse to true on Down,
            // so a Down then Up round-trip is lossy for 3×/4×/5× promos.
            migrationBuilder.AddColumn<bool>(
                name: "DoubleSponsorBonus",
                table: "CorporatePromos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DoubleBuilderBonus",
                table: "CorporatePromos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
                UPDATE CorporatePromos
                SET DoubleSponsorBonus = CASE WHEN SponsorBonusMultiplier >= 2 THEN 1 ELSE 0 END,
                    DoubleBuilderBonus = CASE WHEN BuilderBonusMultiplier >= 2 THEN 1 ELSE 0 END;
            ");

            migrationBuilder.DropColumn(
                name: "SponsorBonusMultiplier",
                table: "CorporatePromos");

            migrationBuilder.DropColumn(
                name: "BuilderBonusMultiplier",
                table: "CorporatePromos");
        }
    }
}
