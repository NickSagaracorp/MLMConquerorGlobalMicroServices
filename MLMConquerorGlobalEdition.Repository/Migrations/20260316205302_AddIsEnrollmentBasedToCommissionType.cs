using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddIsEnrollmentBasedToCommissionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnrollmentBased",
                table: "CommissionTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsEnrollmentBased",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsEnrollmentBased",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "IsEnrollmentBased",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 12,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 14,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 15,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 16,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 17,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 18,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 19,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 20,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 21,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 22,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 23,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 24,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 25,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 26,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 27,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 28,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 29,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 30,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 31,
                column: "IsEnrollmentBased",
                value: false);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 32,
                column: "IsEnrollmentBased",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEnrollmentBased",
                table: "CommissionTypes");
        }
    }
}
