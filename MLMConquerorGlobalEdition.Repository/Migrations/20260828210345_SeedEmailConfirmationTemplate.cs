using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Siembra la plantilla de EmailTemplates para el eventType EMAIL_CONFIRMATION
    /// (NotificationEvents.EmailConfirmation). Sin esta fila, SesEmailService lanza
    /// InvalidOperationException al buscar la plantilla y el correo de confirmación de
    /// dirección no sale nunca: hasta ahora el registro creaba el usuario con
    /// EmailConfirmed = false y nada lo confirmaba salvo los sembradores de desarrollo.
    ///
    /// SOLO en/es. Los otros siete idiomas del catálogo (pt, fr, de, zh, it, kr, ge) quedan sin
    /// sembrar a propósito, por el mismo criterio que SeedTwoFactorTemplates: SesEmailService ya
    /// respalda a "en" cuando falta el idioma pedido, así que la ausencia no rompe nada — solo
    /// entrega en inglés. Una traducción automática de un correo que llega a usuarios reales es
    /// peor que ese respaldo: parece revisada por un hablante nativo y no lo está. Sembrar esos
    /// siete idiomas requiere que alguien que hable cada uno revise el texto antes de que salga
    /// a producción.
    /// </summary>
    public partial class SeedEmailConfirmationTemplate : Migration
    {
        private const int EmailTemplateId       = 9002;
        private const int EmailLocalizationEnId = 9003;
        private const int EmailLocalizationEsId = 9004;

        private const string Actor = "system:seed";

        // Fecha fija y literal: una migración tiene que producir el mismo resultado se aplique
        // cuando se aplique, así que nada de DateTime.Now/UtcNow aquí.
        private static readonly DateTime SeedDate = new(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── EmailTemplates ──────────────────────────────────────────────────
            // Identificadores explícitos y altos, continuando la numeración que abrió
            // SeedTwoFactorTemplates (EmailTemplates 9001, localizaciones 9001/9002): así se ve
            // de un vistazo que son filas de semilla y no algo creado desde la UI de AdminAPI.
            // EF Core envuelve el InsertData en SET IDENTITY_INSERT automáticamente porque la
            // columna Id viene incluida en la lista de columnas.
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Name", "EventType", "Category", "Description", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { EmailTemplateId, "Email Address Confirmation", "EMAIL_CONFIRMATION", "Security", "Sent by SesEmailService so a new user can confirm the email address they signed up with.", true, SeedDate, Actor, null, null });

            // ── EmailTemplateLocalizations ──────────────────────────────────────
            // Variables: {{ConfirmationUrl}} y {{ExpiresInHours}}. La URL ya llega con el token
            // de Identity codificado en base64url desde SendEmailConfirmationHandler.
            //
            // TextBody va relleno: es lo que ven los clientes de correo que no muestran HTML (o
            // que el usuario configuró en texto plano); sin él esos clientes reciben un correo
            // vacío — y aquí un correo vacío es una cuenta que no se puede terminar de activar.
            // En la versión de texto la URL va desnuda porque no hay botón donde esconderla.
            migrationBuilder.InsertData(
                table: "EmailTemplateLocalizations",
                columns: new[] { "Id", "EmailTemplateId", "LanguageCode", "Subject", "HtmlBody", "TextBody", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    {
                        EmailLocalizationEnId, EmailTemplateId, "en",
                        "Confirm your MLM Conqueror email address",
                        EmailHtmlBodyEn,
                        EmailTextBodyEn,
                        SeedDate, Actor, null, null
                    },
                    {
                        EmailLocalizationEsId, EmailTemplateId, "es",
                        "Confirma tu dirección de correo de MLM Conqueror",
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
            Confirm this address to finish setting up your account.
          </td>
        </tr>
        <tr>
          <td align=""center"" style=""padding: 0 0 24px 0;"">
            <a href=""{{ConfirmationUrl}}"" style=""display: inline-block; background-color: #1a73e8; color: #ffffff; font-size: 16px; font-weight: bold; text-decoration: none; padding: 14px 28px; border-radius: 6px;"">Confirm email address</a>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #555555; padding-bottom: 16px; line-height: 1.5; word-break: break-all;"">
            If the button does not work, copy this link into your browser:<br />
            <a href=""{{ConfirmationUrl}}"" style=""color: #1a73e8;"">{{ConfirmationUrl}}</a>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            This link expires in {{ExpiresInHours}} hours.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            If you did not create this account, you can ignore this message.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEn =
            "Confirm this address to finish setting up your account.\n\n" +
            "{{ConfirmationUrl}}\n\n" +
            "This link expires in {{ExpiresInHours}} hours.\n\n" +
            "If you did not create this account, you can ignore this message.";

        private const string EmailHtmlBodyEs = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 24px; line-height: 1.5;"">
            Confirma esta dirección para terminar de configurar tu cuenta.
          </td>
        </tr>
        <tr>
          <td align=""center"" style=""padding: 0 0 24px 0;"">
            <a href=""{{ConfirmationUrl}}"" style=""display: inline-block; background-color: #1a73e8; color: #ffffff; font-size: 16px; font-weight: bold; text-decoration: none; padding: 14px 28px; border-radius: 6px;"">Confirmar dirección de correo</a>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #555555; padding-bottom: 16px; line-height: 1.5; word-break: break-all;"">
            Si el botón no funciona, copia este enlace en tu navegador:<br />
            <a href=""{{ConfirmationUrl}}"" style=""color: #1a73e8;"">{{ConfirmationUrl}}</a>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            Este enlace caduca en {{ExpiresInHours}} horas.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            Si no creaste esta cuenta, puedes ignorar este mensaje.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEs =
            "Confirma esta dirección para terminar de configurar tu cuenta.\n\n" +
            "{{ConfirmationUrl}}\n\n" +
            "Este enlace caduca en {{ExpiresInHours}} horas.\n\n" +
            "Si no creaste esta cuenta, puedes ignorar este mensaje.";
    }
}
