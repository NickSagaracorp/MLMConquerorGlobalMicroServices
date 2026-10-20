using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class BusquedaYNivelDeAlta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_EnrollDate",
                table: "MemberProfiles");

            migrationBuilder.AddColumn<int>(
                name: "SignupMembershipLevelId",
                table: "MemberProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchName",
                table: "MemberProfiles",
                type: "nvarchar(201)",
                maxLength: 201,
                nullable: true,
                computedColumnSql: "CAST(CONCAT([FirstName], N' ', [LastName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchNameReversed",
                table: "MemberProfiles",
                type: "nvarchar(201)",
                maxLength: 201,
                nullable: true,
                computedColumnSql: "CAST(CONCAT([LastName], N' ', [FirstName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_Clientes",
                table: "MemberProfiles",
                column: "MemberId",
                filter: "[MemberType] = 1 AND [IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "EnrollDate", "SponsorMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_EnrollDate",
                table: "MemberProfiles",
                column: "EnrollDate")
                .Annotation("SqlServer:Include", new[] { "SponsorMemberId", "MemberType", "SignupMembershipLevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_SearchName",
                table: "MemberProfiles",
                column: "SearchName")
                .Annotation("SqlServer:Include", new[] { "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_SearchNameReversed",
                table: "MemberProfiles",
                column: "SearchNameReversed")
                .Annotation("SqlServer:Include", new[] { "MemberId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_Clientes",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_EnrollDate",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_SearchName",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_SearchNameReversed",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "SearchName",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "SearchNameReversed",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "SignupMembershipLevelId",
                table: "MemberProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_EnrollDate",
                table: "MemberProfiles",
                column: "EnrollDate");
        }
    }
}
