using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class IndiceResidualesPorBeneficiarioYFecha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate",
                table: "DailyResidualEarnings",
                columns: new[] { "BeneficiaryMemberId", "EarnedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate",
                table: "DailyResidualEarnings");
        }
    }
}
