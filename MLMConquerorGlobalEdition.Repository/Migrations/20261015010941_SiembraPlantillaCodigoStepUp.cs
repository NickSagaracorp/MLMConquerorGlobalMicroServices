using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Siembra las plantillas de correo y SMS STEP_UP_CODE (NotificationEvents.StepUpCode): el código
    /// que confirma un cambio sensible, como a dónde se pagan las comisiones. Hasta ahora el step-up
    /// salía con la plantilla del login, que dice "tu código de verificación" y nada más: quien lo
    /// recibía sin haberlo pedido no tenía forma de saber que alguien estaba reapuntando su cobro.
    ///
    /// Solo en/es, por las mismas razones que SeedTwoFactorTemplates.
    ///
    /// EL SMS EN ESPAÑOL OCUPA DOS SEGMENTOS, y a diferencia del de login aquí se acepta. Lleva tildes
    /// que GSM-7 no cubre (ó, ú), así que va en UCS-2: 70 caracteres en un segmento, 67 por segmento
    /// en un mensaje partido. El aviso "¿no fuiste tú?" no cabe en 70, y en este mensaje es
    /// precisamente lo que importa. El step-up se pide al cambiar el método de cobro, no en cada
    /// inicio de sesión, así que el segundo segmento es un costo menor y raro. Verificado con
    /// {{Code}} = "123456" y {{ExpiresInMinutes}} = "5":
    ///   en: 145 caracteres GSM-7  -> 1 segmento (160)
    ///   es: 131 caracteres UCS-2  -> 2 segmentos (134)
    /// </summary>
    public partial class SiembraPlantillaCodigoStepUp : Migration
    {
        // Siguiente bloque libre tras SiembraPlantillaCambioDeFactor (9008 + 9008/9009).
        private const int EmailTemplateId       = 9010;
        private const int EmailLocalizationEnId = 9010;
        private const int EmailLocalizationEsId = 9011;

        // SmsTemplates solo tiene la 9001 (TWO_FACTOR_CODE) con sus localizaciones 9001/9002.
        private const int SmsTemplateId       = 9002;
        private const int SmsLocalizationEnId = 9003;
        private const int SmsLocalizationEsId = 9004;

        private const string Actor = "system:seed";

        private static readonly DateTime SeedDate = new(2026, 10, 14, 0, 0, 0, 0, DateTimeKind.Unspecified);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Name", "EventType", "Category", "Description", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { EmailTemplateId, "Step-Up Verification Code", "STEP_UP_CODE", "Security", "Sent by TwoFactorService when a member requests a code to confirm a sensitive change (e.g. where commissions are paid).", true, SeedDate, Actor, null, null });

            // Como en el código de login, el código NO va en el asunto: la vista previa de un teléfono
            // bloqueado lo dejaría legible sin desbloquearlo.
            migrationBuilder.InsertData(
                table: "EmailTemplateLocalizations",
                columns: new[] { "Id", "EmailTemplateId", "LanguageCode", "Subject", "HtmlBody", "TextBody", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    {
                        EmailLocalizationEnId, EmailTemplateId, "en",
                        "Confirm a change to your MLM Conqueror account",
                        EmailHtmlBodyEn,
                        EmailTextBodyEn,
                        SeedDate, Actor, null, null
                    },
                    {
                        EmailLocalizationEsId, EmailTemplateId, "es",
                        "Confirma un cambio en tu cuenta de MLM Conqueror",
                        EmailHtmlBodyEs,
                        EmailTextBodyEs,
                        SeedDate, Actor, null, null
                    }
                });

            migrationBuilder.InsertData(
                table: "SmsTemplates",
                columns: new[] { "Id", "Name", "EventType", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { SmsTemplateId, "Step-Up Verification Code", "STEP_UP_CODE", true, SeedDate, Actor, null, null });

            migrationBuilder.InsertData(
                table: "SmsTemplateLocalizations",
                columns: new[] { "Id", "SmsTemplateId", "LanguageCode", "Body", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    { SmsLocalizationEnId, SmsTemplateId, "en", "MLM Conqueror: code {{Code}} confirms a sensitive change to your account, like where you get paid. Expires in {{ExpiresInMinutes}} min. Not you? Change your password.", SeedDate, Actor, null, null },
                    { SmsLocalizationEsId, SmsTemplateId, "es", "MLM Conqueror: código {{Code}} para confirmar un cambio sensible, como dónde cobras. Caduca en {{ExpiresInMinutes}} min. ¿No fuiste tú? Cambia tu clave.", SeedDate, Actor, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "SmsTemplateLocalizations", keyColumn: "Id", keyValue: SmsLocalizationEnId);
            migrationBuilder.DeleteData(table: "SmsTemplateLocalizations", keyColumn: "Id", keyValue: SmsLocalizationEsId);
            migrationBuilder.DeleteData(table: "SmsTemplates", keyColumn: "Id", keyValue: SmsTemplateId);
            migrationBuilder.DeleteData(table: "EmailTemplateLocalizations", keyColumn: "Id", keyValue: EmailLocalizationEnId);
            migrationBuilder.DeleteData(table: "EmailTemplateLocalizations", keyColumn: "Id", keyValue: EmailLocalizationEsId);
            migrationBuilder.DeleteData(table: "EmailTemplates", keyColumn: "Id", keyValue: EmailTemplateId);
        }

        // ── Cuerpos de correo ────────────────────────────────────────────────────
        // Mismo esqueleto que TWO_FACTOR_CODE. Lo que cambia es el texto: dice qué autoriza el código
        // y pide no compartirlo, que es la forma en que un atacante con la sesión obtendría el suyo.

        private const string EmailHtmlBodyEn = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">
            A sensitive change was requested on your MLM Conqueror account, such as changing where your commissions are paid. Use this code to confirm it:
          </td>
        </tr>
        <tr>
          <td align=""center"" style=""padding: 8px 0 24px 0;"">
            <span style=""font-family: 'Courier New', Courier, monospace; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #111111;"">{{Code}}</span>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            This code expires in {{ExpiresInMinutes}} minutes. Never share it with anyone, not even with someone who says they work for MLM Conqueror.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            If you did not request this change, do not use the code. Someone may have access to your account: change your password right away and contact support.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEn =
            "A sensitive change was requested on your MLM Conqueror account, such as changing where your commissions are paid. Use this code to confirm it:\n\n" +
            "{{Code}}\n\n" +
            "This code expires in {{ExpiresInMinutes}} minutes. Never share it with anyone, not even with someone who says they work for MLM Conqueror.\n\n" +
            "If you did not request this change, do not use the code. Someone may have access to your account: change your password right away and contact support.";

        private const string EmailHtmlBodyEs = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr>
          <td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">
            Se solicitó un cambio sensible en tu cuenta de MLM Conqueror, como cambiar a dónde se pagan tus comisiones. Usa este código para confirmarlo:
          </td>
        </tr>
        <tr>
          <td align=""center"" style=""padding: 8px 0 24px 0;"">
            <span style=""font-family: 'Courier New', Courier, monospace; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #111111;"">{{Code}}</span>
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">
            Este código caduca en {{ExpiresInMinutes}} minutos. No lo compartas con nadie, ni siquiera con quien diga que trabaja para MLM Conqueror.
          </td>
        </tr>
        <tr>
          <td style=""font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">
            Si no solicitaste este cambio, no uses el código. Es posible que alguien tenga acceso a tu cuenta: cambia tu contraseña cuanto antes y contacta con soporte.
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEs =
            "Se solicitó un cambio sensible en tu cuenta de MLM Conqueror, como cambiar a dónde se pagan tus comisiones. Usa este código para confirmarlo:\n\n" +
            "{{Code}}\n\n" +
            "Este código caduca en {{ExpiresInMinutes}} minutos. No lo compartas con nadie, ni siquiera con quien diga que trabaja para MLM Conqueror.\n\n" +
            "Si no solicitaste este cambio, no uses el código. Es posible que alguien tenga acceso a tu cuenta: cambia tu contraseña cuanto antes y contacta con soporte.";
    }
}
