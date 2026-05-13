using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberStatisticHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberStatisticHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SnapshotYear = table.Column<int>(type: "int", nullable: false),
                    SnapshotMonth = table.Column<int>(type: "int", nullable: false),
                    PersonalPoints = table.Column<int>(type: "int", nullable: false),
                    ExternalCustomerPoints = table.Column<int>(type: "int", nullable: false),
                    DualTeamSize = table.Column<int>(type: "int", nullable: false),
                    EnrollmentTeamSize = table.Column<int>(type: "int", nullable: false),
                    DualTeamPoints = table.Column<int>(type: "int", nullable: false),
                    EnrollmentPoints = table.Column<int>(type: "int", nullable: false),
                    QualifiedSponsoredMembers = table.Column<int>(type: "int", nullable: false),
                    QualifiedSponsoredExternalCustomers = table.Column<int>(type: "int", nullable: false),
                    EnrollmentTeamGrowth = table.Column<int>(type: "int", nullable: false),
                    DualteamGrowth = table.Column<int>(type: "int", nullable: false),
                    EnrollmentTeamPointsGrowth = table.Column<int>(type: "int", nullable: false),
                    DualTeamPointsGrowth = table.Column<int>(type: "int", nullable: false),
                    CurrentWeekIncomeGrowth = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentMonthIncomeGrowth = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentYearIncomeGrowth = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LeftLegPoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RightLegPoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberStatisticHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatisticHistories_MemberId_SnapshotYear_SnapshotMonth",
                table: "MemberStatisticHistories",
                columns: new[] { "MemberId", "SnapshotYear", "SnapshotMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatisticHistories_SnapshotYear_SnapshotMonth",
                table: "MemberStatisticHistories",
                columns: new[] { "SnapshotYear", "SnapshotMonth" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberStatisticHistories");
        }
    }
}
