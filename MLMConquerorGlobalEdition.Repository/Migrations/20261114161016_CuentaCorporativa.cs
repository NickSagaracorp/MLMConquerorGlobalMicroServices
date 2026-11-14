using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class CuentaCorporativa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCorporateAccount",
                table: "MemberProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCorporateAccount",
                table: "MemberProfiles");
        }
    }
}
