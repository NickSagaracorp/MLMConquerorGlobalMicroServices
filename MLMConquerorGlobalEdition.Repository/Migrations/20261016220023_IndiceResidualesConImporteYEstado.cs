using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class IndiceResidualesConImporteYEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate",
                table: "DailyResidualEarnings");

            migrationBuilder.CreateIndex(
                name: "IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate",
                table: "DailyResidualEarnings",
                columns: new[] { "BeneficiaryMemberId", "EarnedDate" })
                .Annotation("SqlServer:Include", new[] { "Amount", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate",
                table: "DailyResidualEarnings");

            migrationBuilder.CreateIndex(
                name: "IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate",
                table: "DailyResidualEarnings",
                columns: new[] { "BeneficiaryMemberId", "EarnedDate" });
        }
    }
}
