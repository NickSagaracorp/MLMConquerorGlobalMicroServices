using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;

namespace MLMConquerorGlobalEdition.RankEngine.Services;

public class ITextCertificatePdfFillerService : ICertificatePdfFillerService
{
    // The only two AcroForm fields present in every certificate template
    private const string FieldFullName     = "FullName";
    private const string FieldAchievedDate = "AchievedDate";

    private readonly string _templatesFolder;
    private readonly ILogger<ITextCertificatePdfFillerService> _logger;

    public ITextCertificatePdfFillerService(
        IWebHostEnvironment env,
        ILogger<ITextCertificatePdfFillerService> logger)
    {
        _templatesFolder = Path.Combine(env.ContentRootPath, "CertificateTemplates");
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
        using var output  = new MemoryStream();
        using var reader  = new PdfReader(templatePath);
        using var writer  = new PdfWriter(output);
        using var pdfDoc  = new PdfDocument(reader, writer);

        var form = PdfAcroForm.GetAcroForm(pdfDoc, createIfNotExist: false);
        if (form is null)
            throw new InvalidOperationException(
                $"The PDF template at '{templatePath}' has no AcroForm. " +
                "Add fillable fields named 'FullName' and 'AchievedDate' to the template.");

        // FullName — adaptive font size for long names
        SetNameField(form, data.FullName);

        SetField(form, FieldAchievedDate, data.AchievedAt.ToString("MMMM dd, yyyy"));

        form.FlattenFields();
        pdfDoc.Close();
        return output.ToArray();
    }

    /// <summary>
    /// Sets the FullName field with adaptive font sizing so long names fit within the
    /// certificate's name area without overflowing.
    ///
    /// Thresholds (designed for a typical 350px-wide certificate name field at 24pt base):
    ///   ≤ 25 chars  → 24pt  (normal)
    ///   ≤ 35 chars  → 18pt
    ///   ≤ 45 chars  → 14pt
    ///    > 45 chars  → 11pt  (minimum legible)
    /// </summary>
    private static void SetNameField(PdfAcroForm form, string fullName)
    {
        var field = form.GetField(FieldFullName);
        if (field is null) return;

        var fontSize = ComputeNameFontSize(fullName);
        field.SetFontSize(fontSize);
        field.SetValue(fullName);
    }

    private static float ComputeNameFontSize(string fullName)
    {
        var len = fullName.Length;
        if (len <= 25) return 24f;
        if (len <= 35) return 18f;
        if (len <= 45) return 14f;
        return 11f;
    }

    private static void SetField(PdfAcroForm form, string fieldName, string value)
    {
        var field = form.GetField(fieldName);
        if (field is null) return;
        field.SetValue(value);
    }
}
