using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Siembra la plantilla TOKEN_PURCHASE_RECEIPT (NotificationEvents.TokenPurchaseReceipt): el recibo
    /// de una compra de tokens desde el BizCenter, y de una compra en cripto cuando se confirma. Sin la
    /// fila, el transporte lanza al buscarla; TokenPurchaseReceipts lo registra y la compra sigue.
    ///
    /// Solo en/es, como las demás plantillas recientes: el transporte respalda a "en".
    /// Variables: {{FullName}}, {{TokenType}}, {{Quantity}}, {{Amount}}, {{PaymentMethod}},
    /// {{PurchaseDate}} (hora de servidor), {{PurchaseId}}, {{OrderNo}}. Sin códigos de token: son al
    /// portador.
    /// </summary>
    public partial class SiembraPlantillaReciboCompraTokens : Migration
    {
        // Bloque reservado para este recibo; 9016 y 9017 los usa CRYPTO_DEPOSIT_ADDRESS_CHANGED (tarea 3).
        private const int EmailTemplateId       = 9014;
        private const int EmailLocalizationEnId = 9014;
        private const int EmailLocalizationEsId = 9015;

        private const string Actor = "system:seed";

        private static readonly DateTime SeedDate = new(2026, 10, 15, 0, 0, 0, 0, DateTimeKind.Unspecified);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Name", "EventType", "Category", "Description", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { EmailTemplateId, "Token Purchase Receipt", "TOKEN_PURCHASE_RECEIPT", "Billing", "Sent to the member when a token purchase from the BizCenter is completed, or when a crypto token purchase is confirmed. Never includes token codes.", true, SeedDate, Actor, null, null });

            migrationBuilder.InsertData(
                table: "EmailTemplateLocalizations",
                columns: new[] { "Id", "EmailTemplateId", "LanguageCode", "Subject", "HtmlBody", "TextBody", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    { EmailLocalizationEnId, EmailTemplateId, "en", "Your token purchase receipt — order {{OrderNo}}", EmailHtmlBodyEn, EmailTextBodyEn, SeedDate, Actor, null, null },
                    { EmailLocalizationEsId, EmailTemplateId, "es", "Recibo de tu compra de tokens — pedido {{OrderNo}}", EmailHtmlBodyEs, EmailTextBodyEs, SeedDate, Actor, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "EmailTemplateLocalizations", keyColumn: "Id", keyValue: EmailLocalizationEnId);
            migrationBuilder.DeleteData(table: "EmailTemplateLocalizations", keyColumn: "Id", keyValue: EmailLocalizationEsId);
            migrationBuilder.DeleteData(table: "EmailTemplates", keyColumn: "Id", keyValue: EmailTemplateId);
        }

        private const string EmailHtmlBodyEn = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr><td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">Hi {{FullName}}, thanks for your purchase.</td></tr>
        <tr>
          <td style=""font-size: 14px; color: #222222; padding-bottom: 16px; line-height: 1.7;"">
            <strong>Tokens:</strong> {{Quantity}} × {{TokenType}}<br />
            <strong>Total:</strong> {{Amount}}<br />
            <strong>Paid with:</strong> {{PaymentMethod}}<br />
            <strong>Date:</strong> {{PurchaseDate}}<br />
            <strong>Order:</strong> {{OrderNo}}<br />
            <strong>Purchase id:</strong> {{PurchaseId}}
          </td>
        </tr>
        <tr><td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">Your tokens are in the Tokens section of your Biz Center. Token purchases are non-refundable.</td></tr>
        <tr><td style=""font-size: 13px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">If you did not make this purchase, sign in the usual way, change your password and contact support.</td></tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEn =
            "Hi {{FullName}}, thanks for your purchase.\n\n" +
            "Tokens: {{Quantity}} x {{TokenType}}\nTotal: {{Amount}}\nPaid with: {{PaymentMethod}}\nDate: {{PurchaseDate}}\nOrder: {{OrderNo}}\nPurchase id: {{PurchaseId}}\n\n" +
            "Your tokens are in the Tokens section of your Biz Center. Token purchases are non-refundable.\n\n" +
            "If you did not make this purchase, sign in the usual way, change your password and contact support.";

        private const string EmailHtmlBodyEs = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr><td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">Hola {{FullName}}, gracias por tu compra.</td></tr>
        <tr>
          <td style=""font-size: 14px; color: #222222; padding-bottom: 16px; line-height: 1.7;"">
            <strong>Tokens:</strong> {{Quantity}} × {{TokenType}}<br />
            <strong>Total:</strong> {{Amount}}<br />
            <strong>Pagado con:</strong> {{PaymentMethod}}<br />
            <strong>Fecha:</strong> {{PurchaseDate}}<br />
            <strong>Pedido:</strong> {{OrderNo}}<br />
            <strong>Id de compra:</strong> {{PurchaseId}}
          </td>
        </tr>
        <tr><td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">Tus tokens están en la sección Tokens de tu Biz Center. Las compras de tokens no son reembolsables.</td></tr>
        <tr><td style=""font-size: 13px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">Si no hiciste esta compra, entra por el camino de siempre, cambia tu contraseña y contacta con soporte.</td></tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEs =
            "Hola {{FullName}}, gracias por tu compra.\n\n" +
            "Tokens: {{Quantity}} x {{TokenType}}\nTotal: {{Amount}}\nPagado con: {{PaymentMethod}}\nFecha: {{PurchaseDate}}\nPedido: {{OrderNo}}\nId de compra: {{PurchaseId}}\n\n" +
            "Tus tokens están en la sección Tokens de tu Biz Center. Las compras de tokens no son reembolsables.\n\n" +
            "Si no hiciste esta compra, entra por el camino de siempre, cambia tu contraseña y contacta con soporte.";
    }
}
