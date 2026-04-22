using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FixBuilderBonusCrossoverTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 35,
                columns: new[] { "Description", "IsActive" },
                values: new object[] { "Superseded by Cat7 Turbo types (IDs 66-84). Deactivated.", false });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "Description", "IsActive" },
                values: new object[] { "Cat7 only fires for Turbo product (LevelNo=4). Deactivated for VIP.", false });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 37,
                columns: new[] { "Description", "IsActive" },
                values: new object[] { "Cat7 only fires for Turbo product (LevelNo=4). Deactivated for Elite.", false });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 38,
                columns: new[] { "Description", "FixedAmount" },
                values: new object[] { "Builder Bonus Turbo base (rank 0 sponsors). Matches Cat6 base amount.", 60m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 35,
                columns: new[] { "Description", "IsActive" },
                values: new object[] { "Additional sponsor bonus paid when enrolling a Turbo member. Stacks with Member Bonus.", true });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "Description", "IsActive" },
                values: new object[] { "Enhanced Builder Bonus (Turbo program) paid when enrolling a VIP member.", true });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 37,
                columns: new[] { "Description", "IsActive" },
                values: new object[] { "Enhanced Builder Bonus (Turbo program) paid when enrolling an Elite member.", true });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 38,
                columns: new[] { "Description", "FixedAmount" },
                values: new object[] { "Enhanced Builder Bonus (Turbo program) paid when enrolling a Turbo member.", 160m });
        }
    }
}
