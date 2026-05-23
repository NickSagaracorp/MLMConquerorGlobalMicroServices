using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace MLMConquerorGlobalEdition.RankEngine.Services;

/// <summary>
/// Fills a rank certificate by drawing the recipient's name and achievement date
/// directly onto the template PDF (a canvas overlay).
///
/// The 19 certificate templates are pre-designed artwork — they carry the rank name
/// already printed and only leave visual space for the recipient's name and the date.
/// They have no AcroForm fields. All 19 share one layout, so a single set of
/// fractional coordinates positions the text correctly on every rank.
/// </summary>
public class ITextCertificatePdfFillerService : ICertificatePdfFillerService
{
    // ── Layout constants ──────────────────────────────────────────────────────────
    // Positions are fractions of page width/height (PDF origin is bottom-left).
    // Tuned to the shared certificate layout — adjust here if the templates change.

    /// <summary>Recipient name — horizontally centred in the empty name band.</summary>
    private const float NameCenterX = 0.500f;
    private const float NameCenterY = 0.520f;

    /// <summary>
    /// Achievement date — horizontally aligned with the "Date" label that sits below the
    /// gold underline (not the geometric centre of the underline itself), so the eye sees
    /// the date and its label as a single stacked unit.
    /// </summary>
    private const float DateCenterX = 0.776f;
    private const float DateCenterY = 0.165f;

    private const float DateFontSize = 13f;

    private readonly string _templatesFolder;
    private readonly ILogger<ITextCertificatePdfFillerService> _logger;

    public ITextCertificatePdfFillerService(
        string templatesFolder,
        ILogger<ITextCertificatePdfFillerService> logger)
    {
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
        using var output = new MemoryStream();

        using (var reader = new PdfReader(templatePath))
        using (var writer = new PdfWriter(output))
        using (var pdfDoc = new PdfDocument(reader, writer))
        {
            var page     = pdfDoc.GetFirstPage();
            var pageSize = page.GetPageSize();
            var width    = pageSize.GetWidth();
            var height   = pageSize.GetHeight();

            var serifFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            var sansFont  = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            using var canvas = new Canvas(page, pageSize);

            // Recipient name — centred in the name band, size adapts to long names.
            var name = new Paragraph(data.FullName)
                .SetFont(serifFont)
                .SetFontSize(ComputeNameFontSize(data.FullName))
                .SetFontColor(ColorConstants.WHITE);
            canvas.ShowTextAligned(
                name,
                width  * NameCenterX,
                height * NameCenterY,
                TextAlignment.CENTER,
                VerticalAlignment.MIDDLE);

            // Achievement date — centred on the bottom-right signature line.
            var date = new Paragraph(data.AchievedAt.ToString("MMMM dd, yyyy"))
                .SetFont(sansFont)
                .SetFontSize(DateFontSize)
                .SetFontColor(ColorConstants.WHITE);
            canvas.ShowTextAligned(
                date,
                width  * DateCenterX,
                height * DateCenterY,
                TextAlignment.CENTER,
                VerticalAlignment.MIDDLE);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Adaptive font size so long names stay within the certificate's name band.
    ///   ≤ 25 chars → 34pt   ≤ 35 → 27pt   ≤ 45 → 21pt   &gt; 45 → 16pt
    /// </summary>
    private static float ComputeNameFontSize(string fullName)
    {
        var len = fullName.Length;
        if (len <= 25) return 34f;
        if (len <= 35) return 27f;
        if (len <= 45) return 21f;
        return 16f;
    }
}
