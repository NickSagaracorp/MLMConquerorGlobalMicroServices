using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Siembra la plantilla de correo CRYPTO_DEPOSIT_ADDRESS_CHANGED
    /// (NotificationEvents.CryptoDepositAddressChanged): el aviso a cada SuperAdmin activo cuando alguien
    /// crea o cambia una dirección de depósito cripto en Parámetros globales. Sin la fila, SesEmailService
    /// lanza al buscar la plantilla; CryptoDepositAlertNotifier lo registra y el cambio sigue, pero nadie
    /// se enteraría de que los depósitos de los miembros van ahora a otra cartera (riesgo R8 del Plan F).
    ///
    /// Solo correo, y solo en/es, como el resto de plantillas de seguridad: el transporte respalda a
    /// "en" para los demás idiomas.
    ///
    /// Variables: {{Currency}}, {{OldAddress}} (vacío si la dirección se acaba de crear),
    /// {{NewAddress}} (vacío si se dejó de ofrecer la moneda), {{ChangedBy}} y {{ChangedAt}} —hora de
    /// servidor, "yyyy-MM-dd HH:mm"—. Las rellena CryptoDepositAlertNotifier.
    /// </summary>
    public partial class SiembraPlantillaCambioDireccionDeposito : Migration
    {
        // 9014 (plantilla) y 9014/9015 (localizaciones) quedan reservados para TOKEN_PURCHASE_RECEIPT
        // (Plan F, tarea 8), que se siembra después; este es el siguiente bloque libre tras ese.
        private const int EmailTemplateId       = 9016;
        private const int EmailLocalizationEnId = 9016;
        private const int EmailLocalizationEsId = 9017;

        private const string Actor = "system:seed";

        // Fecha fija: una migración tiene que producir lo mismo se aplique cuando se aplique.
        private static readonly DateTime SeedDate = new(2026, 10, 15, 0, 0, 0, 0, DateTimeKind.Unspecified);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Name", "EventType", "Category", "Description", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { EmailTemplateId, "Crypto Deposit Address Changed", "CRYPTO_DEPOSIT_ADDRESS_CHANGED", "Security", "Sent to every active SuperAdmin when a crypto deposit address (Crypto.DepositAddress.*) is created or changed in Global Parameters. Members send their crypto payments to that address.", true, SeedDate, Actor, null, null });

            migrationBuilder.InsertData(
                table: "EmailTemplateLocalizations",
                columns: new[] { "Id", "EmailTemplateId", "LanguageCode", "Subject", "HtmlBody", "TextBody", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    {
                        EmailLocalizationEnId, EmailTemplateId, "en",
                        "{{Currency}} deposit address changed in MLM Conqueror",
                        EmailHtmlBodyEn,
                        EmailTextBodyEn,
                        SeedDate, Actor, null, null
                    },
                    {
                        EmailLocalizationEsId, EmailTemplateId, "es",
                        "Cambió la dirección de depósito {{Currency}} en MLM Conqueror",
                        EmailHtmlBodyEs,
                        EmailTextBodyEs,
                        SeedDate, Actor, null, null
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "EmailTemplateLocalizations", keyColumn: "Id", keyValue: EmailLocalizationEnId);
            migrationBuilder.DeleteData(table: "EmailTemplateLocalizations", keyColumn: "Id", keyValue: EmailLocalizationEsId);
            migrationBuilder.DeleteData(table: "EmailTemplates", keyColumn: "Id", keyValue: EmailTemplateId);
        }

        // ── Cuerpos ──────────────────────────────────────────────────────────────
        // Mismo esqueleto que las demás plantillas de seguridad: tablas y estilos en línea, sin
        // recursos remotos ni enlaces. Las direcciones van en monoespaciada para poder cotejarlas
        // carácter a carácter con la cartera.

        private const string EmailHtmlBodyEn = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""520"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 520px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">
            On {{ChangedAt}} <strong>{{ChangedBy}}</strong> changed the <strong>{{Currency}}</strong> deposit address in Global Parameters. Members paying in {{Currency}} are now shown the new address.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 8px; line-height: 1.5;"">
            Previous address:<br /><span style=""font-family: Consolas, 'Courier New', monospace; color: #222222; word-break: break-all;"">{{OldAddress}}</span>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            New address:<br /><span style=""font-family: Consolas, 'Courier New', monospace; color: #222222; word-break: break-all;"">{{NewAddress}}</span>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            An empty previous address means it was just created; an empty new address means {{Currency}} is no longer offered.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            If nobody on the team expected this change, payments may be going to someone else's wallet. Sign in to the admin panel the usual way, restore the correct address right away and review that account.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEn =
            "On {{ChangedAt}} {{ChangedBy}} changed the {{Currency}} deposit address in Global Parameters. Members paying in {{Currency}} are now shown the new address.\n\n" +
            "Previous address: {{OldAddress}}\n" +
            "New address: {{NewAddress}}\n\n" +
            "An empty previous address means it was just created; an empty new address means {{Currency}} is no longer offered.\n\n" +
            "If nobody on the team expected this change, payments may be going to someone else's wallet. Sign in to the admin panel the usual way, restore the correct address right away and review that account.";

        private const string EmailHtmlBodyEs = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""520"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 520px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">
            El {{ChangedAt}} <strong>{{ChangedBy}}</strong> cambió la dirección de depósito <strong>{{Currency}}</strong> en Parámetros globales. A los miembros que pagan en {{Currency}} ya se les muestra la nueva.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 8px; line-height: 1.5;"">
            Dirección anterior:<br /><span style=""font-family: Consolas, 'Courier New', monospace; color: #222222; word-break: break-all;"">{{OldAddress}}</span>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            Dirección nueva:<br /><span style=""font-family: Consolas, 'Courier New', monospace; color: #222222; word-break: break-all;"">{{NewAddress}}</span>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            Si la dirección anterior está vacía, se acaba de crear; si la nueva está vacía, {{Currency}} deja de ofrecerse.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            Si nadie del equipo esperaba este cambio, los pagos podrían estar llegando a la cartera de otra persona. Entra al panel de administración por el camino de siempre, restablece la dirección correcta cuanto antes y revisa esa cuenta.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEs =
            "El {{ChangedAt}} {{ChangedBy}} cambió la dirección de depósito {{Currency}} en Parámetros globales. A los miembros que pagan en {{Currency}} ya se les muestra la nueva.\n\n" +
            "Dirección anterior: {{OldAddress}}\n" +
            "Dirección nueva: {{NewAddress}}\n\n" +
            "Si la dirección anterior está vacía, se acaba de crear; si la nueva está vacía, {{Currency}} deja de ofrecerse.\n\n" +
            "Si nadie del equipo esperaba este cambio, los pagos podrían estar llegando a la cartera de otra persona. Entra al panel de administración por el camino de siempre, restablece la dirección correcta cuanto antes y revisa esa cuenta.";
    }
}
