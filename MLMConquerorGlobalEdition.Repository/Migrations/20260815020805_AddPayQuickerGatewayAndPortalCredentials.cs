using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPayQuickerGatewayAndPortalCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminPortalUrl",
                table: "PaymentGateways",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiVersion",
                table: "PaymentGateways",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "PaymentGateways",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalPasswordEncrypted",
                table: "ApiCredentials",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalUrl",
                table: "ApiCredentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalUsernameEncrypted",
                table: "ApiCredentials",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AdminPortalUrl", "ApiVersion", "Environment" },
                values: new object[] { "https://www.i-payout.com/", null, "Sandbox" });

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AdminPortalUrl", "ApiVersion", "Environment" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AdminPortalUrl", "ApiVersion", "Environment" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "AdminPortalUrl", "ApiVersion", "Environment" },
                values: new object[] { "https://account.volet.com/", null, null });

            migrationBuilder.InsertData(
                table: "PaymentGateways",
                columns: new[] { "Id", "AdminFee", "AdminFeeKind", "AdminPortalUrl", "ApiVersion", "CreatedBy", "CreationDate", "Currency", "Description", "DisplayName", "Environment", "IsActive", "LastUpdateBy", "LastUpdateDate", "MinAdminFee", "MinimumPayoutAmount", "WalletType" },
                values: new object[] { 5, 1.95m, 1, "https://sandbox.payquicker.io/", "V2", "seed", new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "PayQuicker delivers your commissions to an insured account linked to a debit card. You receive an invitation email to complete registration; once verified, funds are available instantly and can be spent online, at retail, or moved to your bank. Admin fee: $1.95 USD per transaction.", "PayQuicker", "Sandbox", false, null, null, null, 25m, 11 });

            // Filas de credencial para PayQuicker (una por versión y ambiente, porque los
            // scopes de v1 y v2 no son intercambiables) y para i-Payout.
            //
            // Van acá y no sólo en GatewayRoutingSeeder porque ese seeder se saltea el bloque
            // entero si YA existe alguna ApiCredential — o sea, no alcanza a las bases ya
            // sembradas, que son todas menos una instalación nueva.
            //
            // Se crean vacías e IsActive = 0: los secretos se cargan desde
            // /admin/billing/credentials, nunca desde una migración versionada.
            // El NOT EXISTS respeta el índice único (ServiceKey, Environment).
            migrationBuilder.Sql(@"
INSERT INTO [ApiCredentials] ([Id],[ServiceKey],[Environment],[BaseUrl],[PortalUrl],[IsActive],[IsDeleted],[CreationDate],[LastUpdateDate],[CreatedBy])
SELECT CONVERT(nvarchar(50), NEWID()), v.ServiceKey, v.Environment, v.BaseUrl, v.PortalUrl, 0, 0,
       SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration:AddPayQuickerGatewayAndPortalCredentials'
FROM (VALUES
    (N'PayQuickerV2', N'Sandbox',    N'https://api.sandbox.payquicker.io/api/v2', N'https://sandbox.payquicker.io/'),
    (N'PayQuickerV2', N'Production', N'https://api.payquicker.io/api/v2',         N'https://portal.payquicker.io/'),
    (N'PayQuickerV1', N'Sandbox',    N'https://platform.mypayquicker.build',      N'https://mypayquicker.build/'),
    (N'PayQuickerV1', N'Production', N'https://platform.mypayquicker.com',        N'https://mypayquicker.com/'),
    (N'EWallet',      N'Sandbox',    N'https://sandbox.i-payout.com/eWalletAPI',  N'https://sandbox.i-payout.com/'),
    (N'EWallet',      N'Production', N'https://api.i-payout.com/eWalletAPI',      N'https://www.i-payout.com/')
) AS v(ServiceKey, Environment, BaseUrl, PortalUrl)
WHERE NOT EXISTS (
    SELECT 1 FROM [ApiCredentials] c
    WHERE c.[ServiceKey] = v.ServiceKey AND c.[Environment] = v.Environment);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sólo se borran las credenciales que creó esta migración y que siguen vacías.
            // Si alguien ya cargó los secretos, la fila se conserva: perder una credencial
            // de producción por un rollback de esquema sería mucho peor que dejar una fila
            // huérfana.
            migrationBuilder.Sql(@"
DELETE FROM [ApiCredentials]
WHERE [CreatedBy] = N'migration:AddPayQuickerGatewayAndPortalCredentials'
  AND [ApiKeyEncrypted] IS NULL
  AND [SecretKeyEncrypted] IS NULL
  AND [MerchantIdEncrypted] IS NULL
  AND [AdditionalSecretEncrypted] IS NULL
  AND [PortalUsernameEncrypted] IS NULL
  AND [PortalPasswordEncrypted] IS NULL;");

            migrationBuilder.DeleteData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "AdminPortalUrl",
                table: "PaymentGateways");

            migrationBuilder.DropColumn(
                name: "ApiVersion",
                table: "PaymentGateways");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "PaymentGateways");

            migrationBuilder.DropColumn(
                name: "PortalPasswordEncrypted",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "PortalUrl",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "PortalUsernameEncrypted",
                table: "ApiCredentials");
        }
    }
}
