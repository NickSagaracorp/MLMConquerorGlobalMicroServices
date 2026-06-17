using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberStatisticDeltaQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberStatisticDeltas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnrollmentPointsDelta = table.Column<int>(type: "int", nullable: false),
                    EnrollmentTeamSizeDelta = table.Column<int>(type: "int", nullable: false),
                    QualifiedSponsoredMembersDelta = table.Column<int>(type: "int", nullable: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceMemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberStatisticDeltas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatisticDeltas_IsApplied_CreationDate",
                table: "MemberStatisticDeltas",
                columns: new[] { "IsApplied", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatisticDeltas_MemberId_IsApplied",
                table: "MemberStatisticDeltas",
                columns: new[] { "MemberId", "IsApplied" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberStatisticDeltas");
        }
    }
}
