using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddContestPointsBoxPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defaults match the legacy MWR-Life banner template's white
            // placeholder (right-of-center, 75% / 45%). Plain banners with
            // a centered box can be reconfigured to 50/50 from the admin
            // form per contest.
            migrationBuilder.AddColumn<int>(
                name: "PointsBoxXPercent",
                table: "CorporateContests",
                type: "int",
                nullable: false,
                defaultValue: 75);

            migrationBuilder.AddColumn<int>(
                name: "PointsBoxYPercent",
                table: "CorporateContests",
                type: "int",
                nullable: false,
                defaultValue: 45);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsBoxXPercent",
                table: "CorporateContests");

            migrationBuilder.DropColumn(
                name: "PointsBoxYPercent",
                table: "CorporateContests");
        }
    }
}
