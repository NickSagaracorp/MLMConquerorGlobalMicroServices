using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Siembra la plantilla de correo SECURITY_FACTOR_CHANGED (NotificationEvents.SecurityFactorChanged):
    /// el aviso al dueño cuando cambia un canal por el que recibe códigos. Sin la fila, SesEmailService
    /// lanza al buscar la plantilla; SecurityFactorNotifier lo registra y el cambio sigue, pero el aviso
    /// —que es lo que le da sentido a la espera de 24 h del step-up— no saldría nunca.
    ///
    /// Solo correo, y solo en/es, por las mismas razones que SeedTwoFactorTemplates: el transporte
    /// respalda a "en" para el resto de idiomas, y un mensaje de seguridad traducido sin revisar es peor
    /// que ese respaldo. No hay SMS: el teléfono puede ser justo lo que cambió.
    ///
    /// Variables: {{Change}} —qué cambió, ya en el idioma de la plantilla— y {{ChangedAt}} —hora de
    /// servidor, "yyyy-MM-dd HH:mm"—. Las rellena SecurityFactorNotifier.
    /// </summary>
    public partial class SiembraPlantillaCambioDeFactor : Migration
    {
        // Siguiente bloque libre tras SeedPasswordResetTemplate (9005 + 9006/9007).
        private const int EmailTemplateId       = 9008;
        private const int EmailLocalizationEnId = 9008;
        private const int EmailLocalizationEsId = 9009;

        private const string Actor = "system:seed";

        // Fecha fija: una migración tiene que producir lo mismo se aplique cuando se aplique.
        private static readonly DateTime SeedDate = new(2026, 10, 14, 0, 0, 0, 0, DateTimeKind.Unspecified);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Name", "EventType", "Category", "Description", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { EmailTemplateId, "Security Factor Changed", "SECURITY_FACTOR_CHANGED", "Security", "Sent to the account owner when the email, confirmed phone, authenticator or preferred 2FA channel changes. Email changes go to the previous address.", true, SeedDate, Actor, null, null });

            // El asunto no dice qué cambió: en la vista previa de un teléfono bloqueado basta con que
            // diga que hubo un cambio de seguridad, y el detalle exige abrir el correo.
            migrationBuilder.InsertData(
                table: "EmailTemplateLocalizations",
                columns: new[] { "Id", "EmailTemplateId", "LanguageCode", "Subject", "HtmlBody", "TextBody", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    {
                        EmailLocalizationEnId, EmailTemplateId, "en",
                        "Security change on your MLM Conqueror account",
                        EmailHtmlBodyEn,
                        EmailTextBodyEn,
                        SeedDate, Actor, null, null
                    },
                    {
                        EmailLocalizationEsId, EmailTemplateId, "es",
                        "Cambio de seguridad en tu cuenta de MLM Conqueror",
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
        // recursos remotos. Ningún enlace: un correo de seguridad con un botón es exactamente lo que
        // imita el phishing, y aquí lo que se pide es entrar por el camino de siempre.

        private const string EmailHtmlBodyEn = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">
            On {{ChangedAt}} the following was changed on your MLM Conqueror account: <strong>{{Change}}</strong>.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            If you made this change, you don't need to do anything. For your protection, sensitive changes such as where your commissions are paid are blocked for a while.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            If you did not make this change, someone may have access to your account. Sign in the usual way, change your password right away and contact support.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEn =
            "On {{ChangedAt}} the following was changed on your MLM Conqueror account: {{Change}}.\n\n" +
            "If you made this change, you don't need to do anything. For your protection, sensitive changes such as where your commissions are paid are blocked for a while.\n\n" +
            "If you did not make this change, someone may have access to your account. Sign in the usual way, change your password right away and contact support.";

        private const string EmailHtmlBodyEs = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">
            El {{ChangedAt}} se cambió en tu cuenta de MLM Conqueror: <strong>{{Change}}</strong>.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            Si fuiste tú, no tienes que hacer nada. Para protegerte, los cambios sensibles, como a dónde se pagan tus comisiones, quedan bloqueados durante un tiempo.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            Si no fuiste tú, es posible que alguien tenga acceso a tu cuenta. Entra por el camino de siempre, cambia tu contraseña cuanto antes y contacta con soporte.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEs =
            "El {{ChangedAt}} se cambió en tu cuenta de MLM Conqueror: {{Change}}.\n\n" +
            "Si fuiste tú, no tienes que hacer nada. Para protegerte, los cambios sensibles, como a dónde se pagan tus comisiones, quedan bloqueados durante un tiempo.\n\n" +
            "Si no fuiste tú, es posible que alguien tenga acceso a tu cuenta. Entra por el camino de siempre, cambia tu contraseña cuanto antes y contacta con soporte.";
    }
}
