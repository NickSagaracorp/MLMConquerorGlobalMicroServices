using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public class ITextReceiptPdfRenderer : IReceiptPdfRenderer
{
    public byte[] Render(PayoutReceiptData d)
    {
        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        using (var pdf = new PdfDocument(writer))
        using (var doc = new Document(pdf))
        {
            var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            doc.Add(new Paragraph("Payment Receipt")
                .SetFont(bold).SetFontSize(20).SetTextAlignment(TextAlignment.CENTER));

            doc.Add(new Paragraph($"Receipt ID: {d.PayoutAttemptId}").SetFont(regular).SetFontSize(10));
            doc.Add(new Paragraph($"Ambassador: {d.FullName} ({d.MemberId})").SetFont(regular).SetFontSize(10));
            doc.Add(new Paragraph($"Payment method: {d.WalletType}").SetFont(regular).SetFontSize(10));
            doc.Add(new Paragraph($"Payout account: {d.PayoutAccountSnapshot}").SetFont(regular).SetFontSize(10));
            doc.Add(new Paragraph($"Gateway transaction: {d.GatewayTransactionId ?? "-"}").SetFont(regular).SetFontSize(10));
            doc.Add(new Paragraph($"Process date (UTC): {d.ProcessDateUtc:yyyy-MM-dd}").SetFont(regular).SetFontSize(10));
            doc.Add(new Paragraph($"Paid at (UTC): {d.CompletedAtUtc:yyyy-MM-dd HH:mm:ss}").SetFont(regular).SetFontSize(10));
            doc.Add(new Paragraph($"Total paid: USD {d.AmountUsd:F2}").SetFont(bold).SetFontSize(12));

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 70, 30 })).UseAllAvailableWidth();
            table.AddHeaderCell("Commission Earning");
            table.AddHeaderCell("Amount (USD)");
            foreach (var e in d.Earnings)
            {
                table.AddCell(e.CommissionEarningId);
                table.AddCell(e.Amount.ToString("F2"));
            }
            doc.Add(table);

            doc.Add(new Paragraph(
                "This receipt is recorded in a tamper-evident ledger. Authenticity can be verified by the issuer.")
                .SetFontSize(8).SetMarginTop(20));
        }
        return ms.ToArray();
    }
}
