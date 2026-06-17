using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UnifyPayoutGatewayOnPaymentGatewayInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutGatewaySettings");

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumPayoutAmount",
                table: "PaymentGateways",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 1,
                column: "MinimumPayoutAmount",
                value: 25m);

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 2,
                column: "MinimumPayoutAmount",
                value: 25m);

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 3,
                column: "MinimumPayoutAmount",
                value: 25m);

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 4,
                column: "MinimumPayoutAmount",
                value: 25m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumPayoutAmount",
                table: "PaymentGateways");

            migrationBuilder.CreateTable(
                name: "PayoutGatewaySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MinimumPayoutAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WalletType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutGatewaySettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutGatewaySettings_WalletType",
                table: "PayoutGatewaySettings",
                column: "WalletType",
                unique: true);
        }
    }
}
