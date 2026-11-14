using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class IndiceDeGananciasPorFecha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CommissionEarnings_EarnedDate_Beneficiary",
                table: "CommissionEarnings",
                columns: new[] { "EarnedDate", "BeneficiaryMemberId" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Amount", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommissionEarnings_EarnedDate_Beneficiary",
                table: "CommissionEarnings");
        }
    }
}
