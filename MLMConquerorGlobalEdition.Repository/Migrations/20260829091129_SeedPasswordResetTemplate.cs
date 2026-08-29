using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Siembra la plantilla de EmailTemplates para el eventType PASSWORD_RESET
    /// (NotificationEvents.PasswordReset). Sin esta fila, SesEmailService lanza
    /// InvalidOperationException al buscar la plantilla y el correo de recuperación de contraseña
    /// no sale nunca — que es exactamente lo que llevaba pasando desde el principio: el handler
    /// generaba el token de Identity y lo tiraba con un TODO, así que nadie ha recuperado nunca su
    /// contraseña por su cuenta.
    ///
    /// SOLO en/es. Los otros siete idiomas del catálogo (pt, fr, de, zh, it, kr, ge) quedan sin
    /// sembrar a propósito, por el mismo criterio que SeedEmailConfirmationTemplate:
    /// SesEmailService ya respalda a "en" cuando falta el idioma pedido, así que la ausencia no
    /// rompe nada — solo entrega en inglés. Una traducción automática de un correo que llega a
    /// usuarios reales es peor que ese respaldo: parece revisada por un hablante nativo y no lo
    /// está. Sembrar esos siete idiomas requiere que alguien que hable cada uno revise el texto
    /// antes de que salga a producción.
    /// </summary>
    public partial class SeedPasswordResetTemplate : Migration
    {
        private const int EmailTemplateId       = 9005;
        private const int EmailLocalizationEnId = 9006;
        private const int EmailLocalizationEsId = 9007;

        private const string Actor = "system:seed";

        // Fecha fija y literal: una migración tiene que producir el mismo resultado se aplique
        // cuando se aplique, así que nada de DateTime.Now/UtcNow aquí.
        private static readonly DateTime SeedDate = new(2026, 8, 29, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── EmailTemplates ──────────────────────────────────────────────────
            // Identificadores explícitos y altos, continuando la numeración que abrieron
            // SeedTwoFactorTemplates (9001) y SeedEmailConfirmationTemplate (9002 + 9003/9004):
            // así se ve de un vistazo que son filas de semilla y no algo creado desde la UI de
            // AdminAPI. EF Core envuelve el InsertData en SET IDENTITY_INSERT automáticamente
            // porque la columna Id viene incluida en la lista de columnas.
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Name", "EventType", "Category", "Description", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { EmailTemplateId, "Password Reset", "PASSWORD_RESET", "Security", "Sent by SesEmailService so a user who forgot their password can set a new one.", true, SeedDate, Actor, null, null });

            // ── EmailTemplateLocalizations ──────────────────────────────────────
            // Variables: {{ResetUrl}} y {{ExpiresInHours}}. La URL ya llega con el token de
            // Identity codificado en base64url desde ForgotPasswordHandler, y lleva userId en vez
            // de email: una dirección de correo en la query se queda en el historial del
            // navegador, en los registros de cualquier proxy y en la cabecera Referer.
            //
            // TextBody va relleno: es lo que ven los clientes de correo que no muestran HTML (o
            // que el usuario configuró en texto plano); sin él esos clientes reciben un correo
            // vacío — y aquí un correo vacío es un usuario que no puede volver a entrar. En la
            // versión de texto la URL va desnuda porque no hay botón donde esconderla.
            migrationBuilder.InsertData(
                table: "EmailTemplateLocalizations",
                columns: new[] { "Id", "EmailTemplateId", "LanguageCode", "Subject", "HtmlBody", "TextBody", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    {
                        EmailLocalizationEnId, EmailTemplateId, "en",
                        "Reset your MLM Conqueror password",
                        EmailHtmlBodyEn,
                        EmailTextBodyEn,
                        SeedDate, Actor, null, null
                    },
                    {
                        EmailLocalizationEsId, EmailTemplateId, "es",
                        "Restablece tu contraseña de MLM Conqueror",
                        EmailHtmlBodyEs,
                        EmailTextBodyEs,
                        SeedDate, Actor, null, null
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hijos primero, igual que insertaron: aunque la FK es ON DELETE CASCADE, borrar
            // explícito deja el Down simétrico con lo que hizo el Up y no depende de que el
            // motor de base de datos cascadee.
            migrationBuilder.DeleteData(
                table: "EmailTemplateLocalizations",
                keyColumn: "Id",
                keyValue: EmailLocalizationEnId);

            migrationBuilder.DeleteData(
                table: "EmailTemplateLocalizations",
                keyColumn: "Id",
                keyValue: EmailLocalizationEsId);

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: EmailTemplateId);
        }

        // ── Cuerpos de correo ────────────────────────────────────────────────────
        // HTML simple con estilos en línea y tablas, nada de CSS externo (los clientes de
        // correo lo descartan) ni de recursos remotos.

        private const string EmailHtmlBodyEn = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 24px; line-height: 1.5;"">
            We received a request to reset the password for your account.
          </td>
        </tr>
        <tr>
          <td align=""center"" style=""padding: 0 0 24px 0;"">
            <a href=""{{ResetUrl}}"" style=""display: inline-block; background-color: #1a73e8; color: #ffffff; font-size: 16px; font-weight: bold; text-decoration: none; padding: 14px 28px; border-radius: 6px;"">Reset password</a>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #555555; padding-bottom: 16px; line-height: 1.5; word-break: break-all;"">
            If the button does not work, copy this link into your browser:<br />
            <a href=""{{ResetUrl}}"" style=""color: #1a73e8;"">{{ResetUrl}}</a>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            This link expires in {{ExpiresInHours}} hours.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            If you did not request this, you can ignore this message; your password will not change.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEn =
            "We received a request to reset the password for your account.\n\n" +
            "{{ResetUrl}}\n\n" +
            "This link expires in {{ExpiresInHours}} hours.\n\n" +
            "If you did not request this, you can ignore this message; your password will not change.";

        private const string EmailHtmlBodyEs = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 24px; line-height: 1.5;"">
            Hemos recibido una solicitud para restablecer la contraseña de tu cuenta.
          </td>
        </tr>
        <tr>
          <td align=""center"" style=""padding: 0 0 24px 0;"">
            <a href=""{{ResetUrl}}"" style=""display: inline-block; background-color: #1a73e8; color: #ffffff; font-size: 16px; font-weight: bold; text-decoration: none; padding: 14px 28px; border-radius: 6px;"">Restablecer contraseña</a>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #555555; padding-bottom: 16px; line-height: 1.5; word-break: break-all;"">
            Si el botón no funciona, copia este enlace en tu navegador:<br />
            <a href=""{{ResetUrl}}"" style=""color: #1a73e8;"">{{ResetUrl}}</a>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            Este enlace caduca en {{ExpiresInHours}} horas.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            Si no lo solicitaste, puedes ignorar este mensaje; tu contraseña no cambiará.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEs =
            "Hemos recibido una solicitud para restablecer la contraseña de tu cuenta.\n\n" +
            "{{ResetUrl}}\n\n" +
            "Este enlace caduca en {{ExpiresInHours}} horas.\n\n" +
            "Si no lo solicitaste, puedes ignorar este mensaje; tu contraseña no cambiará.";
    }
}
