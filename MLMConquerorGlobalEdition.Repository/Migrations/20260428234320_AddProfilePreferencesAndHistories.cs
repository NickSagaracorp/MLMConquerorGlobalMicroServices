using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilePreferencesAndHistories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultLanguage",
                table: "MemberProfiles",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<int>(
                name: "PayoutFrequency",
                table: "MemberProfiles",
                type: "int",
                nullable: false,
                defaultValue: 2);  // 2 = Weekly

            migrationBuilder.CreateTable(
                name: "MemberAddressHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviousCity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviousState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviousZipCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviousCountry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewCity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewZipCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewCountry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberAddressHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberCredentialChangeLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberCredentialChangeLogs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberAddressHistories");

            migrationBuilder.DropTable(
                name: "MemberCredentialChangeLogs");

            migrationBuilder.DropColumn(
                name: "DefaultLanguage",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "PayoutFrequency",
                table: "MemberProfiles");
        }
    }
}
