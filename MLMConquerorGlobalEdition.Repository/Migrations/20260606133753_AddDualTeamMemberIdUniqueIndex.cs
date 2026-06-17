using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDualTeamMemberIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DualTeamTree_MemberId",
                table: "DualTeamTree");

            migrationBuilder.CreateIndex(
                name: "IX_DualTeamTree_MemberId",
                table: "DualTeamTree",
                column: "MemberId",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DualTeamTree_MemberId",
                table: "DualTeamTree");

            migrationBuilder.CreateIndex(
                name: "IX_DualTeamTree_MemberId",
                table: "DualTeamTree",
                column: "MemberId");
        }
    }
}
