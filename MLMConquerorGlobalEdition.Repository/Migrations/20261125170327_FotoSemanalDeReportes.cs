using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FotoSemanalDeReportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EarningsRankingSnapshots",
                columns: table => new
                {
                    SnapshotDate = table.Column<DateTime>(type: "date", nullable: false),
                    WindowDays = table.Column<int>(type: "int", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MemberName = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarningsRankingSnapshots", x => new { x.SnapshotDate, x.WindowDays, x.Rank });
                });

            migrationBuilder.CreateTable(
                name: "RankStandingSnapshots",
                columns: table => new
                {
                    SnapshotDate = table.Column<DateTime>(type: "date", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RankDefinitionId = table.Column<int>(type: "int", nullable: false),
                    RankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RankSortOrder = table.Column<int>(type: "int", nullable: false),
                    Since = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankStandingSnapshots", x => new { x.SnapshotDate, x.Mode, x.Position });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EarningsRankingSnapshots");

            migrationBuilder.DropTable(
                name: "RankStandingSnapshots");
        }
    }
}
