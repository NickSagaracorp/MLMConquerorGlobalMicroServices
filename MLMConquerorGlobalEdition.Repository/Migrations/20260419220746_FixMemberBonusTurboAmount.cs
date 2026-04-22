using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FixMemberBonusTurboAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "FixedAmount" },
                values: new object[] { "One-time $40 bonus paid to the direct enroller for the Turbo portion. Stacks with Member Bonus – Elite ($40) for a combined $80 on Turbo signups.", 40m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "FixedAmount" },
                values: new object[] { "One-time $80 bonus to the direct enroller when a Turbo member signs up.", 80m });
        }
    }
}
