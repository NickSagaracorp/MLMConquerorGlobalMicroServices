using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class IndicesDeFiltrosDeMiembros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "MemberProfiles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_Email",
                table: "MemberProfiles",
                column: "Email",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_FirstName",
                table: "MemberProfiles",
                column: "FirstName",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_LastName",
                table: "MemberProfiles",
                column: "LastName",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_Phone",
                table: "MemberProfiles",
                column: "Phone",
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_Email",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_FirstName",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_LastName",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_Phone",
                table: "MemberProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "MemberProfiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);
        }
    }
}
