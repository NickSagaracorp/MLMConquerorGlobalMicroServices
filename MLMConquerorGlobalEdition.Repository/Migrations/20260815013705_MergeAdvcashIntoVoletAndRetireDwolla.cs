using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Dos cambios de gateway que viajan juntos porque ambos aterrizan en Volet:
    ///
    /// 1. AdvCash se renombró a Volet. Es el MISMO proveedor, así que el viejo
    ///    WalletType.Advancash (10) se fusiona en WalletType.Volet (6) — que además es el
    ///    id que usa MWRLife y al que ya apuntaban VoletPayoutGatewayService y
    ///    VoletPayoutCsvAdapter. Por ser un renombre y no un cambio de proveedor, el
    ///    historial y los logs de API se mueven con las wallets.
    ///
    /// 2. Dwolla se retira. Sus wallets se mueven a Volet, que también paga contra el
    ///    email, así que el AccountIdentifier sigue siendo válido y no hay que pedirle
    ///    nada al ambassador. Acá SÍ hay cambio de proveedor, por eso el historial viejo
    ///    se deja diciendo "Dwolla" y se escribe una fila WalletTypeChanged por cada
    ///    wallet movida. El valor 1 del enum se conserva justamente para eso.
    /// </summary>
    public partial class MergeAdvcashIntoVoletAndRetireDwolla : Migration
    {
        private const int Dwolla    = 1;
        private const int Volet     = 6;
        private const int Advancash = 10;
        private const string Actor  = "migration:MergeAdvcashIntoVoletAndRetireDwolla";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. AdvCash (10) → Volet (6): renombre del mismo proveedor ────────────
            // Se mueve todo, incluido el historial: para el ambassador siempre fue la
            // misma cuenta, sólo que la empresa cambió de nombre.
            migrationBuilder.Sql($"UPDATE [Wallets]               SET [WalletType] = {Volet} WHERE [WalletType] = {Advancash};");
            migrationBuilder.Sql($"UPDATE [WalletHistories]       SET [WalletType] = {Volet} WHERE [WalletType] = {Advancash};");
            migrationBuilder.Sql($"UPDATE [WalletApiLogs]         SET [WalletType] = {Volet} WHERE [WalletType] = {Advancash};");
            migrationBuilder.Sql($"UPDATE [CountryPayoutDefaults] SET [WalletType] = {Volet} WHERE [WalletType] = {Advancash};");
            migrationBuilder.Sql($"UPDATE [PayoutBatches]         SET [WalletType] = {Volet} WHERE [WalletType] = {Advancash};");

            // ── 2. Dwolla (1) → Volet (6): cambio de proveedor ───────────────────────
            // El INSERT va ANTES del UPDATE para capturar el estado previo de cada wallet.
            // WalletType = tipo NUEVO y ChangeReason con formato "X → Y", igual que hace
            // AdminPayoutDefaultsController al reasignar gateways retroactivamente.
            migrationBuilder.Sql($@"
INSERT INTO [WalletHistories]
    ([WalletId], [MemberId], [WalletType], [Action], [OldStatus], [NewStatus],
     [OldAccountIdentifier], [NewAccountIdentifier], [OldIsPreferred], [NewIsPreferred],
     [ChangeReason], [CreationDate], [CreatedBy])
SELECT w.[Id], w.[MemberId], {Volet}, 7, w.[Status], w.[Status],
       w.[AccountIdentifier], w.[AccountIdentifier], w.[IsPreferred], w.[IsPreferred],
       N'Dwolla → Volet: Dwolla retired. Both gateways pay out to the same email address, so the account identifier is unchanged.',
       SYSUTCDATETIME(), N'{Actor}'
FROM [Wallets] w
WHERE w.[WalletType] = {Dwolla};");

            migrationBuilder.Sql($"UPDATE [Wallets]               SET [WalletType] = {Volet} WHERE [WalletType] = {Dwolla};");
            migrationBuilder.Sql($"UPDATE [CountryPayoutDefaults] SET [WalletType] = {Volet} WHERE [WalletType] = {Dwolla};");

            // WalletHistories y WalletApiLogs con Dwolla NO se tocan: son el registro de lo
            // que efectivamente pasó por ese gateway y reescribirlos borraría la auditoría.

            // ── 3. Seed de PaymentGateways (generado por EF) ─────────────────────────
            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "DisplayName", "IsActive" },
                values: new object[] { "Dwolla is no longer offered. Existing accounts were migrated to Volet, which also pays out to your email address. This entry is kept only so historical payout records keep resolving.", "Dwolla (retired)", false });

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "DisplayName", "WalletType" },
                values: new object[] { "Volet (formerly AdvCash) holds funds in your Volet account in the currency of your choice. From there you can withdraw to bank, card, or other gateways. Verification through Volet is required to lift withdrawal limits. Available in most regions worldwide. Admin fee: $1.95 USD per transaction.", "Volet", 6 });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "DisplayName", "IsActive" },
                values: new object[] { "Dwolla pushes commissions directly into your linked US bank account. You must complete Dwolla's identity verification before your account is approved. Standard ACH transfers settle in 3–5 business days. Dwolla is US-only. Admin fee: $1.95 USD per transaction.", "Dwolla", true });

            migrationBuilder.UpdateData(
                table: "PaymentGateways",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "DisplayName", "WalletType" },
                values: new object[] { "AdvCash holds funds in your AdvCash account in the currency of your choice. From there you can withdraw to bank, card, or other gateways. Verification through AdvCash is required to lift withdrawal limits. Available in most regions worldwide. Admin fee: $1.95 USD per transaction.", "AdvCash", 10 });

            // Las wallets movidas de Dwolla sí se pueden restaurar con precisión, porque las
            // filas de historial que escribió Up() dicen exactamente cuáles fueron.
            migrationBuilder.Sql($@"
UPDATE w SET w.[WalletType] = {Dwolla}
FROM [Wallets] w
WHERE EXISTS (SELECT 1 FROM [WalletHistories] h
              WHERE h.[WalletId] = w.[Id] AND h.[CreatedBy] = N'{Actor}');");

            migrationBuilder.Sql($"DELETE FROM [WalletHistories] WHERE [CreatedBy] = N'{Actor}';");

            // La fusión AdvCash → Volet NO se revierte: una vez fusionadas no hay forma de
            // distinguir qué wallets de Volet vinieron del 10 y cuáles ya eran 6, y además
            // el valor 10 ya no existe en WalletType — restaurarlo dejaría filas que el
            // dominio no sabe mapear. Si hiciera falta revertir, hay que hacerlo a mano
            // desde un backup previo a esta migración.
        }
    }
}
