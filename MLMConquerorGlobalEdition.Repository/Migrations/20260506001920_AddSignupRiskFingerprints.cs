using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSignupRiskFingerprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignupRiskFingerprints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Flow = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    MemberId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    SponsorReplicateSite = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CountryIso2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsFlagged = table.Column<bool>(type: "bit", nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignupRiskFingerprints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignupRiskFingerprints_IpAddress_CreationDate",
                table: "SignupRiskFingerprints",
                columns: new[] { "IpAddress", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SignupRiskFingerprints_OrderId",
                table: "SignupRiskFingerprints",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRiskFingerprints_VisitorId_CreationDate",
                table: "SignupRiskFingerprints",
                columns: new[] { "VisitorId", "CreationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignupRiskFingerprints");
        }
    }
}
