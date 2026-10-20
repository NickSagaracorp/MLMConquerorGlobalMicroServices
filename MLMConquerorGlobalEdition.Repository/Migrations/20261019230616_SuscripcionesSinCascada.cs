using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SuscripcionesSinCascada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                table: "MembershipSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId",
                table: "MembershipSubscriptions");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                table: "MembershipSubscriptions",
                column: "MemberId",
                principalTable: "MemberProfiles",
                principalColumn: "MemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId",
                table: "MembershipSubscriptions",
                column: "MembershipLevelId",
                principalTable: "MembershipLevels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                table: "MembershipSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId",
                table: "MembershipSubscriptions");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                table: "MembershipSubscriptions",
                column: "MemberId",
                principalTable: "MemberProfiles",
                principalColumn: "MemberId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId",
                table: "MembershipSubscriptions",
                column: "MembershipLevelId",
                principalTable: "MembershipLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
