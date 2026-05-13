using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyResidualEarningSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentRankId",
                table: "DailyResidualEarnings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EligibleDualTeamPoints",
                table: "DailyResidualEarnings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EligibleEnrollmentTeamPoints",
                table: "DailyResidualEarnings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonalPoints",
                table: "DailyResidualEarnings",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentRankId",
                table: "DailyResidualEarnings");

            migrationBuilder.DropColumn(
                name: "EligibleDualTeamPoints",
                table: "DailyResidualEarnings");

            migrationBuilder.DropColumn(
                name: "EligibleEnrollmentTeamPoints",
                table: "DailyResidualEarnings");

            migrationBuilder.DropColumn(
                name: "PersonalPoints",
                table: "DailyResidualEarnings");
        }
    }
}
