using MLMConquerorGlobalEdition.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace MLMConquerorGlobalEdition.RankEngine.Services;

/// <summary>
/// Fills a rank certificate by drawing the recipient's name and achievement date
/// directly onto the template PDF (an overlay on the first page), with PDFsharp.
///
/// The 19 certificate templates are pre-designed artwork — they carry the rank name
/// already printed and only leave visual space for the recipient's name and the date.
/// They have no AcroForm fields. All 19 share one layout, so a single set of
/// fractional coordinates positions the text correctly on every rank.
/// </summary>
public class CertificatePdfFillerService : ICertificatePdfFillerService
{
    // ── Layout constants ──────────────────────────────────────────────────────────
    // Positions are fractions of page width/height measured from the BOTTOM-left corner,
    // as in PDF user space (they were tuned that way with iText). XGraphics draws from the
    // top-left, so the Y fraction is flipped when drawing.
    // Tuned to the shared certificate layout — adjust here if the templates change.

    /// <summary>Recipient name — horizontally centred in the empty name band.</summary>
    private const double NameCenterX = 0.500;
    private const double NameCenterY = 0.520;

    /// <summary>
    /// Achievement date — horizontally aligned with the "Date" label that sits below the
    /// gold underline (not the geometric centre of the underline itself), so the eye sees
    /// the date and its label as a single stacked unit.
    /// </summary>
    private const double DateCenterX = 0.776;
    private const double DateCenterY = 0.165;

    private const double DateFontSize = 13;

    private readonly string _templatesFolder;
    private readonly ILogger<CertificatePdfFillerService> _logger;

    public CertificatePdfFillerService(
        string templatesFolder,
        ILogger<CertificatePdfFillerService> logger)
    {
        PdfFonts.EnsureRegistered();
        _templatesFolder = templatesFolder;
        _logger          = logger;
    }

    public async Task<byte[]> FillAsync(
        int rankSortOrder,
        CertificateTemplateData data,
        CancellationToken ct = default)
    {
        var templatePath = ResolveTemplatePath(rankSortOrder);
        _logger.LogDebug("Using certificate template: {Path}", templatePath);

        return await Task.Run(() => FillTemplate(templatePath, data), ct);
    }

    /// <summary>
    /// Resolves the template file by rank sort-order number.
    /// Template files are named by their rank sequence: 1.pdf (Silver), 2.pdf, 3.pdf, …
    /// </summary>
    private string ResolveTemplatePath(int rankSortOrder)
    {
        var path = Path.Combine(_templatesFolder, $"{rankSortOrder}.pdf");
        if (File.Exists(path)) return path;

        throw new FileNotFoundException(
            $"No certificate template found for rank sort-order {rankSortOrder}. " +
            $"Expected file: '{path}'.");
    }

    private static byte[] FillTemplate(string templatePath, CertificateTemplateData data)
    {
        using var document = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        var page = document.Pages[0];

        using (var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
        {
            var width  = gfx.PageSize.Width;
            var height = gfx.PageSize.Height;

            // Recipient name — centred in the name band, size adapts to long names.
            var nameFont = new XFont(PdfFonts.Serif, ComputeNameFontSize(data.FullName));
            gfx.DrawString(
                data.FullName, nameFont, XBrushes.White,
                new XPoint(width * NameCenterX, height * (1 - NameCenterY)),
                XStringFormats.Center);

            // Achievement date — centred on the bottom-right signature line.
            var dateFont = new XFont(PdfFonts.Sans, DateFontSize);
            gfx.DrawString(
                data.AchievedAt.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture),
                dateFont, XBrushes.White,
                new XPoint(width * DateCenterX, height * (1 - DateCenterY)),
                XStringFormats.Center);
        }

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    /// <summary>
    /// Adaptive font size so long names stay within the certificate's name band.
    ///   ≤ 25 chars → 34pt   ≤ 35 → 27pt   ≤ 45 → 21pt   &gt; 45 → 16pt
    /// </summary>
    private static double ComputeNameFontSize(string fullName)
    {
        var len = fullName.Length;
        if (len <= 25) return 34;
        if (len <= 35) return 27;
        if (len <= 45) return 21;
        return 16;
    }
}
