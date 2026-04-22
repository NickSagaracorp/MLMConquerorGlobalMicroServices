using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSsnEncryptedAndCountryHasStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SsnEncrypted",
                table: "MemberProfiles",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasStates",
                table: "Countries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            var countriesWithStates = new[] { "US", "CA", "AU", "BR", "MX", "IN", "AR", "DE", "ES", "IT", "GB", "MY", "NG", "ZA", "RU", "PT", "CL", "CO", "VE" };
            var inClause = string.Join(", ", countriesWithStates.Select(c => $"'{c}'"));
            migrationBuilder.Sql($"UPDATE Countries SET HasStates = 1 WHERE Iso2 IN ({inClause})");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SsnEncrypted",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "HasStates",
                table: "Countries");
        }
    }
}
