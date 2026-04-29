using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRankRequirementsToCanonical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EnrollmentTeam", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "RankDescription", "TeamPoints" },
                values: new object[] { 18, 0.66000000000000003, 0.0, "Qualify with 18 Enrollment Team points (max 66% per leg).", 0 });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "EnrollmentTeam", "MaxTeamPointsPerBranch", "RankDescription", "TeamPoints" },
                values: new object[] { 72, 0.0, "Qualify with 72 Enrollment Team points (max 50% per leg).", 0 });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "EnrollmentTeam", "MaxTeamPointsPerBranch", "RankDescription", "TeamPoints" },
                values: new object[] { 175, 0.0, "Qualify with 175 Enrollment Team points (max 50% per leg). Boost Bonus unlocked.", 0 });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 175, "Qualify with 350 Dual Team points and 175 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 350, "Qualify with 700 Dual Team and 350 Enrollment Team points (max 50% per leg). Presidential Bonus unlocked." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 750, "Qualify with 1,500 Dual Team and 750 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 1500, "Qualify with 3,000 Dual Team and 1,500 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 3000, "Qualify with 6,000 Dual Team and 3,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 5000, "Qualify with 10,000 Dual Team and 5,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 7500, "Qualify with 15,000 Dual Team and 7,500 Enrollment Team points (max 50% per leg). Car Bonus unlocked." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 10000, "Qualify with 20,000 Dual Team and 10,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 15000, "Qualify with 30,000 Dual Team and 15,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 30000, "Qualify with 60,000 Dual Team and 30,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 60000, "Qualify with 120,000 Dual Team and 60,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 100000, "Qualify with 200,000 Dual Team and 100,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 150000, "Qualify with 300,000 Dual Team and 150,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 200000, "Qualify with 400,000 Dual Team and 200,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 250000, "Qualify with 500,000 Dual Team and 250,000 Enrollment Team points (max 50% per leg)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 350000, "Qualify with 700,000 Dual Team and 350,000 Enrollment Team points (max 50% per leg). The pinnacle of the Ambassador journey." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EnrollmentTeam", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "RankDescription", "TeamPoints" },
                values: new object[] { 3, 0.5, 0.5, "Qualify with 18 Enrollment Team points (3 Elite/Turbo members, max 50% per branch).", 18 });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "EnrollmentTeam", "MaxTeamPointsPerBranch", "RankDescription", "TeamPoints" },
                values: new object[] { 12, 0.5, "Qualify with 72 Enrollment Team points (12 Elite/Turbo members, max 50% per branch).", 72 });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "EnrollmentTeam", "MaxTeamPointsPerBranch", "RankDescription", "TeamPoints" },
                values: new object[] { 29, 0.5, "Qualify with 175 Enrollment Team points (max 50% per branch). Boost Bonus unlocked.", 175 });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 350 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 700 Dual Team points (max 50% per branch). Presidential Bonus unlocked." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 1,500 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 3,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 6,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 10,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 15,000 Dual Team points (max 50% per branch). Car Bonus unlocked." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 20,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 30,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 60,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 120,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 200,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 300,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 400,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 500,000 Dual Team points (max 50% per branch)." });

            migrationBuilder.UpdateData(
                table: "RankRequirements",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "EnrollmentTeam", "RankDescription" },
                values: new object[] { 0, "Qualify with 700,000 Dual Team points (max 50% per branch). The pinnacle of the Ambassador journey." });
        }
    }
}
