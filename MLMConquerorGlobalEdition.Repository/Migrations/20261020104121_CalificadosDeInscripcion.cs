using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class CalificadosDeInscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EnrollmentQualifiedTeamMembers",
                table: "MemberStatistics",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EnrollmentQualifiedTeamMembersDelta",
                table: "MemberStatisticDeltas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnrollmentQualifiedTeamMembers",
                table: "MemberStatistics");

            migrationBuilder.DropColumn(
                name: "EnrollmentQualifiedTeamMembersDelta",
                table: "MemberStatisticDeltas");
        }
    }
}
