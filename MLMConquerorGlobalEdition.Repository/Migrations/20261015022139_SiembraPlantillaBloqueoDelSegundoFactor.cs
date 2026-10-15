using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Siembra la plantilla de correo TWO_FACTOR_LOCKED (NotificationEvents.TwoFactorLocked): el aviso al
    /// dueño cuando se alcanza el tope diario de códigos fallidos y el login o el step-up quedan
    /// bloqueados. Sin la fila, SesEmailService lanza al buscar la plantilla; SecurityFactorNotifier lo
    /// registra y el bloqueo se aplica igual, pero el dueño no se enteraría de que alguien prueba sus
    /// códigos.
    ///
    /// Solo correo, y solo en/es, por las mismas razones que SiembraPlantillaCambioDeFactor: el
    /// transporte respalda a "en" para el resto de idiomas, y un mensaje de seguridad traducido sin
    /// revisar es peor que ese respaldo. No hay SMS: si lo que se prueba son códigos de SMS, el teléfono
    /// es justo el canal en el que no conviene fiarse.
    ///
    /// Variables: {{Action}} —qué quedó bloqueado, ya en el idioma de la plantilla— y {{LockedAt}}
    /// —hora de servidor, "yyyy-MM-dd HH:mm"—. Las rellena SecurityFactorNotifier.
    /// </summary>
    public partial class SiembraPlantillaBloqueoDelSegundoFactor : Migration
    {
        // Siguiente bloque libre tras SiembraPlantillaCodigoStepUp (9010 + 9010/9011).
        private const int EmailTemplateId       = 9012;
        private const int EmailLocalizationEnId = 9012;
        private const int EmailLocalizationEsId = 9013;

        private const string Actor = "system:seed";

        // Fecha fija: una migración tiene que producir lo mismo se aplique cuando se aplique.
        private static readonly DateTime SeedDate = new(2026, 10, 14, 0, 0, 0, 0, DateTimeKind.Unspecified);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Name", "EventType", "Category", "Description", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { EmailTemplateId, "Two-Factor Locked", "TWO_FACTOR_LOCKED", "Security", "Sent once to the account owner when the daily cap of wrong verification codes is reached and sign-in or step-up verification is blocked for up to 24 hours.", true, SeedDate, Actor, null, null });

            // Como en el aviso de cambio de factor, el asunto no da detalles: en la vista previa de un
            // teléfono bloqueado basta con que diga que hubo una alerta de seguridad.
            migrationBuilder.InsertData(
                table: "EmailTemplateLocalizations",
                columns: new[] { "Id", "EmailTemplateId", "LanguageCode", "Subject", "HtmlBody", "TextBody", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    {
                        EmailLocalizationEnId, EmailTemplateId, "en",
                        "Security alert on your MLM Conqueror account",
                        EmailHtmlBodyEn,
                        EmailTextBodyEn,
                        SeedDate, Actor, null, null
                    },
                    {
                        EmailLocalizationEsId, EmailTemplateId, "es",
                        "Alerta de seguridad en tu cuenta de MLM Conqueror",
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
            On {{LockedAt}} too many incorrect verification codes were entered to <strong>{{Action}}</strong> on your MLM Conqueror account.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            For your protection, that verification is blocked for up to 24 hours. Codes cannot be requested or used until then, not even the correct one.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            If it wasn't you, someone knows your password or has access to an open session. Sign in the usual way once the block ends, change your password right away and contact support.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEn =
            "On {{LockedAt}} too many incorrect verification codes were entered to {{Action}} on your MLM Conqueror account.\n\n" +
            "For your protection, that verification is blocked for up to 24 hours. Codes cannot be requested or used until then, not even the correct one.\n\n" +
            "If it wasn't you, someone knows your password or has access to an open session. Sign in the usual way once the block ends, change your password right away and contact support.";

        private const string EmailHtmlBodyEs = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">
            El {{LockedAt}} se introdujeron demasiados códigos de verificación incorrectos para <strong>{{Action}}</strong> en tu cuenta de MLM Conqueror.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            Para protegerte, esa verificación queda bloqueada hasta 24 horas. Mientras tanto no se pueden pedir ni usar códigos, ni siquiera el correcto.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            Si no fuiste tú, alguien conoce tu contraseña o tiene acceso a una sesión abierta. Cuando termine el bloqueo, entra por el camino de siempre, cambia tu contraseña cuanto antes y contacta con soporte.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEs =
            "El {{LockedAt}} se introdujeron demasiados códigos de verificación incorrectos para {{Action}} en tu cuenta de MLM Conqueror.\n\n" +
            "Para protegerte, esa verificación queda bloqueada hasta 24 horas. Mientras tanto no se pueden pedir ni usar códigos, ni siquiera el correcto.\n\n" +
            "Si no fuiste tú, alguien conoce tu contraseña o tiene acceso a una sesión abierta. Cuando termine el bloqueo, entra por el camino de siempre, cambia tu contraseña cuanto antes y contacta con soporte.";
    }
}
