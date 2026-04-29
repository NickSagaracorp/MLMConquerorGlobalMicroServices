using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentGatewayInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous AddPaymentGatewayInfo migration (timestamp 20260428193800)
            // was removed from disk during local development before being committed,
            // but it had already been applied to the dev database. Drop the legacy
            // artefacts so this canonical migration can recreate the table cleanly.
            migrationBuilder.Sql(
                "IF OBJECT_ID('PaymentGateways', 'U') IS NOT NULL DROP TABLE PaymentGateways;");
            migrationBuilder.Sql(
                "DELETE FROM __EFMigrationsHistory WHERE MigrationId = '20260428193800_AddPaymentGatewayInfo';");

            migrationBuilder.CreateTable(
                name: "PaymentGateways",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletType = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AdminFee = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AdminFeeKind = table.Column<int>(type: "int", nullable: false),
                    MinAdminFee = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentGateways", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PaymentGateways",
                columns: new[] { "Id", "AdminFee", "AdminFeeKind", "CreatedBy", "CreationDate", "Currency", "Description", "DisplayName", "IsActive", "LastUpdateBy", "LastUpdateDate", "MinAdminFee", "WalletType" },
                values: new object[,]
                {
                    { 1, 1.95m, 1, "seed", new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "I-Payout maintains your in-account balance. Once you register, I-Payout sends a confirmation email; you must verify before payouts can be sent. Funds typically arrive within 24 hours of approval. International withdrawals from your I-Payout account to a bank may incur additional fees from I-Payout itself. Admin fee: $1.95 USD per transaction.", "eWallet (I-Payout)", true, null, null, null, 4 },
                    { 2, 1.95m, 1, "seed", new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "Dwolla pushes commissions directly into your linked US bank account. You must complete Dwolla's identity verification before your account is approved. Standard ACH transfers settle in 3–5 business days. Dwolla is US-only. Admin fee: $1.95 USD per transaction.", "Dwolla", true, null, null, null, 1 },
                    { 3, 2.00m, 2, "seed", new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "Provide the receiving wallet address for Bitcoin (BTC) or USDT (TRC-20). Double-check the address — crypto transactions are irreversible. The company is not liable for funds sent to a wrong address you provided. Network fees are deducted from the payout in addition to the admin fee. Admin fee: minimum 2% of payout, with a minimum of $6.95 USD per transaction.", "Crypto (Bitcoin / USDT)", true, null, null, 6.95m, 9 },
                    { 4, 1.95m, 1, "seed", new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "AdvCash holds funds in your AdvCash account in the currency of your choice. From there you can withdraw to bank, card, or other gateways. Verification through AdvCash is required to lift withdrawal limits. Available in most regions worldwide. Admin fee: $1.95 USD per transaction.", "AdvCash", true, null, null, null, 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentGateways_WalletType",
                table: "PaymentGateways",
                column: "WalletType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentGateways");
        }
    }
}
