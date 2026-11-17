using System.Globalization;
using MLMConquerorGlobalEdition.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

/// <summary>
/// Recibo de pago en PDF con PDFsharp: encabezado, datos del pago, tabla de las comisiones pagadas
/// y la nota de autenticidad. Carta, márgenes de una pulgada; la tabla sigue en páginas nuevas, con
/// su encabezado repetido, cuando no cabe en una.
/// </summary>
public class ReceiptPdfRenderer : IReceiptPdfRenderer
{
    private const double Margin       = 72;
    private const double RowHeight    = 18;
    private const double AmountColumn = 0.70;   // la columna del importe empieza al 70 % del ancho

    private static readonly XColor Border = XColor.FromArgb(160, 160, 160);
    private static readonly XColor Header = XColor.FromArgb(235, 235, 235);

    public ReceiptPdfRenderer() => PdfFonts.EnsureRegistered();

    public byte[] Render(PayoutReceiptData d)
    {
        var title   = new XFont(PdfFonts.Sans, 20, XFontStyleEx.Bold);
        var regular = new XFont(PdfFonts.Sans, 10);
        var bold    = new XFont(PdfFonts.Sans, 10, XFontStyleEx.Bold);
        var total   = new XFont(PdfFonts.Sans, 12, XFontStyleEx.Bold);
        var small   = new XFont(PdfFonts.Sans, 8);

        using var document = new PdfDocument();
        document.Info.Title = $"Payment Receipt {d.PayoutAttemptId}";

        var page  = new Page(document);
        var width = page.ContentWidth;

        page.Gfx.DrawString("Payment Receipt", title, XBrushes.Black,
            new XRect(Margin, page.Y, width, 26), XStringFormats.TopCenter);
        page.Y += 40;

        page.Line($"Receipt ID: {d.PayoutAttemptId}", regular);
        page.Line($"Ambassador: {d.FullName} ({d.MemberId})", regular);
        page.Line($"Payment method: {d.WalletType}", regular);
        page.Line($"Payout account: {d.PayoutAccountSnapshot}", regular);
        page.Line($"Gateway transaction: {d.GatewayTransactionId ?? "-"}", regular);
        page.Line($"Process date (UTC): {d.ProcessDateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", regular);
        page.Line($"Paid at (UTC): {d.CompletedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}", regular);
        page.Line($"Total paid: USD {d.AmountUsd.ToString("F2", CultureInfo.InvariantCulture)}", total);
        page.Y += 10;

        page.TableHeader(bold);
        foreach (var e in d.Earnings)
        {
            if (page.Y + RowHeight > page.Bottom)
            {
                page = page.Next();
                page.TableHeader(bold);
            }
            page.Row(e.CommissionEarningId, e.Amount.ToString("F2", CultureInfo.InvariantCulture), regular, fill: null);
        }

        page.Y += 20;
        if (page.Y + 24 > page.Bottom) page = page.Next();
        new XTextFormatter(page.Gfx).DrawString(
            "This receipt is recorded in a tamper-evident ledger. Authenticity can be verified by the issuer.",
            small, XBrushes.Black, new XRect(Margin, page.Y, width, 24), XStringFormats.TopLeft);

        page.Gfx.Dispose();

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    /// <summary>Una página carta con su cursor vertical.</summary>
    private sealed class Page
    {
        private readonly PdfDocument _document;

        public Page(PdfDocument document)
        {
            _document = document;
            var page  = document.AddPage();
            page.Width  = XUnit.FromPoint(612);
            page.Height = XUnit.FromPoint(792);
            Gfx          = XGraphics.FromPdfPage(page);
            ContentWidth = page.Width.Point - 2 * Margin;
            Bottom       = page.Height.Point - Margin;
            Y            = Margin;
        }

        public XGraphics Gfx          { get; }
        public double    ContentWidth { get; }
        public double    Bottom       { get; }
        public double    Y            { get; set; }

        public Page Next()
        {
            Gfx.Dispose();
            return new Page(_document);
        }

        public void Line(string text, XFont font)
        {
            var height = font.GetHeight() + 4;
            Gfx.DrawString(text, font, XBrushes.Black,
                new XRect(Margin, Y, ContentWidth, height), XStringFormats.TopLeft);
            Y += height;
        }

        public void TableHeader(XFont font) =>
            Row("Commission Earning", "Amount (USD)", font, fill: new XSolidBrush(Header));

        public void Row(string left, string right, XFont font, XBrush? fill)
        {
            var split = ContentWidth * AmountColumn;
            var pen   = new XPen(Border, 0.5);
            var leftCell  = new XRect(Margin, Y, split, RowHeight);
            var rightCell = new XRect(Margin + split, Y, ContentWidth - split, RowHeight);

            if (fill is not null)
            {
                Gfx.DrawRectangle(fill, leftCell);
                Gfx.DrawRectangle(fill, rightCell);
            }
            Gfx.DrawRectangle(pen, leftCell);
            Gfx.DrawRectangle(pen, rightCell);

            Gfx.DrawString(left, font, XBrushes.Black, Inset(leftCell), XStringFormats.CenterLeft);
            Gfx.DrawString(right, font, XBrushes.Black, Inset(rightCell), XStringFormats.CenterLeft);
            Y += RowHeight;
        }

        private static XRect Inset(XRect cell) =>
            new(cell.X + 4, cell.Y, cell.Width - 8, cell.Height);
    }
}
