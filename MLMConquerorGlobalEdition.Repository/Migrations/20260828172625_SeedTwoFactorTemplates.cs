using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Siembra las plantillas de EmailTemplates/SmsTemplates para el eventType TWO_FACTOR_CODE
    /// (NotificationEvents.TwoFactorCode). Sin estas filas, SesEmailService y TwilioSmsService
    /// lanzan InvalidOperationException al buscar la plantilla y TwoFactorService.IssueAsync
    /// devuelve CHANNEL_UNAVAILABLE para cualquier canal Email o Sms — era la tercera razón,
    /// junto al transporte nulo y un bug de validación ya resueltos, por la que el 2FA nunca
    /// llegaba a entregar un código real.
    ///
    /// SOLO en/es. Los otros siete idiomas del catálogo (pt, fr, de, zh, it, kr, ge) quedan
    /// sin sembrar a propósito: SesEmailService y TwilioSmsService ya respaldan a "en" cuando
    /// falta el idioma pedido (ver DefaultLanguage en ambos servicios), así que la ausencia no
    /// rompe nada, solo entrega en inglés. Una traducción automática de un mensaje de seguridad
    /// que llega a usuarios reales es peor que ese respaldo: parece revisada por un hablante
    /// nativo y no lo está. Sembrar esos siete idiomas requiere que alguien que hable cada uno
    /// revise el texto antes de que salga a producción.
    ///
    /// El SMS no lleva el aviso "si no fuiste tú, cambia tu contraseña" que sí lleva el correo.
    /// No es un olvido: es una decisión de costo. Ver el comentario sobre GSM-7/UCS-2 más abajo.
    /// </summary>
    public partial class SeedTwoFactorTemplates : Migration
    {
        private const int EmailTemplateId   = 9001;
        private const int EmailLocalizationEnId = 9001;
        private const int EmailLocalizationEsId = 9002;

        private const int SmsTemplateId     = 9001;
        private const int SmsLocalizationEnId = 9001;
        private const int SmsLocalizationEsId = 9002;

        private const string Actor = "system:seed";

        // Fecha fija y literal: una migración tiene que producir el mismo resultado se aplique
        // cuando se aplique, así que nada de DateTime.Now/UtcNow aquí.
        private static readonly DateTime SeedDate = new(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── EmailTemplates ──────────────────────────────────────────────────
            // Las tablas EmailTemplates/SmsTemplates usan identidad autogenerada (ver
            // AppDbContextModelSnapshot: ValueGeneratedOnAdd + UseIdentityColumn) y hoy están
            // vacías, así que no hay un "siguiente Id" del que colgarse como hacen otras
            // migraciones de seed que continúan un catálogo ya poblado (p.ej.
            // SeedRankSeniorityBonusCommissionTypes, AddFullTokenTypeCatalog). Se fijan
            // identificadores explícitos altos (9001+) para que sea obvio que son filas de
            // semilla y no algo que pudo haber creado la UI de AdminAPI; EF Core envuelve el
            // InsertData en SET IDENTITY_INSERT automáticamente porque la columna Id viene
            // incluida en la lista de columnas.
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Name", "EventType", "Category", "Description", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { EmailTemplateId, "Two-Factor Verification Code", "TWO_FACTOR_CODE", "Security", "Sent by SesEmailService when a member requests a 2FA code by email during login.", true, SeedDate, Actor, null, null });

            // ── EmailTemplateLocalizations ──────────────────────────────────────
            // El código NO va en el asunto a propósito: en un teléfono bloqueado la vista
            // previa de la notificación muestra el asunto, y ponerlo ahí lo haría legible sin
            // desbloquear el teléfono — justo el factor que el 2FA intenta añadir. El cuerpo sí
            // exige abrir el correo.
            //
            // TextBody va relleno: es lo que ven los clientes de correo que no muestran HTML
            // (o que el usuario configuró en texto plano); sin él esos clientes reciben un
            // correo vacío.
            migrationBuilder.InsertData(
                table: "EmailTemplateLocalizations",
                columns: new[] { "Id", "EmailTemplateId", "LanguageCode", "Subject", "HtmlBody", "TextBody", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    {
                        EmailLocalizationEnId, EmailTemplateId, "en",
                        "Your MLM Conqueror verification code",
                        EmailHtmlBodyEn,
                        EmailTextBodyEn,
                        SeedDate, Actor, null, null
                    },
                    {
                        EmailLocalizationEsId, EmailTemplateId, "es",
                        "Tu código de verificación de MLM Conqueror",
                        EmailHtmlBodyEs,
                        EmailTextBodyEs,
                        SeedDate, Actor, null, null
                    }
                });

            // ── SmsTemplates ─────────────────────────────────────────────────────
            migrationBuilder.InsertData(
                table: "SmsTemplates",
                columns: new[] { "Id", "Name", "EventType", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { SmsTemplateId, "Two-Factor Verification Code", "TWO_FACTOR_CODE", true, SeedDate, Actor, null, null });

            // ── SmsTemplateLocalizations ─────────────────────────────────────────
            // Por qué el texto es tan corto: un SMS en GSM-7 admite 160 caracteres por
            // segmento, pero ese alfabeto NO incluye ó/í/á/ú (sí é, ñ, ü, à, ¿, ¡). En cuanto
            // el texto lleva una tilde que GSM-7 no cubre — como la "ó" de "código" — Twilio
            // manda el mensaje entero en UCS-2, que son solo 70 caracteres por segmento. Un
            // mensaje en español typeado sin cuidado (p.ej. "tu código de verificación es...")
            // pasa de 70 y se parte en dos segmentos, el doble de costo en cada inicio de
            // sesión legítimo con 2FA por SMS.
            //
            // La solución no es quitarle los acentos al español (queda mal escrito, poco
            // profesional) sino redactarlo para que quepa en 70 caracteres ya sustituido.
            // Verificado con {{Code}} = "123456" y {{ExpiresInMinutes}} = "5":
            //   en: "MLM Conqueror: your code is 123456. Expires in 5 min."  -> 53 caracteres
            //   es: "MLM Conqueror: tu código es 123456. Caduca en 5 min."   -> 53 caracteres
            // Ambos caben en un segmento (70 en es por UCS-2, 160 en en por GSM-7).
            //
            // Por eso tampoco lleva el aviso de seguridad ("si no fuiste tú...") que sí lleva
            // el correo: no cabe en un segmento sin recortar el mensaje ya de por sí ajustado.
            // Es una decisión de costo, no un olvido.
            migrationBuilder.InsertData(
                table: "SmsTemplateLocalizations",
                columns: new[] { "Id", "SmsTemplateId", "LanguageCode", "Body", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    { SmsLocalizationEnId, SmsTemplateId, "en", "MLM Conqueror: your code is {{Code}}. Expires in {{ExpiresInMinutes}} min.", SeedDate, Actor, null, null },
                    { SmsLocalizationEsId, SmsTemplateId, "es", "MLM Conqueror: tu código es {{Code}}. Caduca en {{ExpiresInMinutes}} min.", SeedDate, Actor, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hijos primero, igual que insertaron: aunque las FKs son ON DELETE CASCADE, borrar
            // explícito deja el Down simétrico con lo que hizo el Up y no depende de que el
            // motor de base de datos cascadee.
            migrationBuilder.DeleteData(
                table: "SmsTemplateLocalizations",
                keyColumn: "Id",
                keyValue: SmsLocalizationEnId);

            migrationBuilder.DeleteData(
                table: "SmsTemplateLocalizations",
                keyColumn: "Id",
                keyValue: SmsLocalizationEsId);

            migrationBuilder.DeleteData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: SmsTemplateId);

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
          <td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">
            A verification code was requested for your MLM Conqueror account.
          </td>
        </tr>
        <tr>
          <td align=""center"" style=""padding: 8px 0 24px 0;"">
            <span style=""font-family: 'Courier New', Courier, monospace; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #111111;"">{{Code}}</span>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            This code expires in {{ExpiresInMinutes}} minutes.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            If you did not try to sign in, someone may know your password. Change it as soon as you can.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEn =
            "A verification code was requested for your MLM Conqueror account.\n\n" +
            "{{Code}}\n\n" +
            "This code expires in {{ExpiresInMinutes}} minutes.\n\n" +
            "If you did not try to sign in, someone may know your password. Change it as soon as you can.";

        private const string EmailHtmlBodyEs = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">
            Se solicitó un código de verificación para tu cuenta de MLM Conqueror.
          </td>
        </tr>
        <tr>
          <td align=""center"" style=""padding: 8px 0 24px 0;"">
            <span style=""font-family: 'Courier New', Courier, monospace; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #111111;"">{{Code}}</span>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            Este código caduca en {{ExpiresInMinutes}} minutos.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            Si no intentaste iniciar sesión, es posible que alguien conozca tu contraseña. Cámbiala cuanto antes.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEs =
            "Se solicitó un código de verificación para tu cuenta de MLM Conqueror.\n\n" +
            "{{Code}}\n\n" +
            "Este código caduca en {{ExpiresInMinutes}} minutos.\n\n" +
            "Si no intentaste iniciar sesión, es posible que alguien conozca tu contraseña. Cámbiala cuanto antes.";
    }
}
