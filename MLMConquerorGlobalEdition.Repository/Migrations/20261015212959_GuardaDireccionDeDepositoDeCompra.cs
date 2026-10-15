using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class GuardaDireccionDeDepositoDeCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepositAddress",
                table: "CryptoPaymentConfirmations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositNetwork",
                table: "CryptoPaymentConfirmations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositAddress",
                table: "CryptoPaymentConfirmations");

            migrationBuilder.DropColumn(
                name: "DepositNetwork",
                table: "CryptoPaymentConfirmations");
        }
    }
}
