using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;

namespace MLMConquerorGlobalEdition.RankEngine.Services;

public class ITextCertificatePdfFillerService : ICertificatePdfFillerService
{
    // AcroForm field names expected in every certificate PDF template
    private const string FieldFullName    = "FullName";
    private const string FieldAchievedDate = "AchievedDate";

    private readonly string _templatesFolder;
    private readonly ILogger<ITextCertificatePdfFillerService> _logger;

    public ITextCertificatePdfFillerService(IWebHostEnvironment env, ILogger<ITextCertificatePdfFillerService> logger)
    {
        _templatesFolder = Path.Combine(env.ContentRootPath, "CertificateTemplates");
        _logger = logger;
    }

    public async Task<byte[]> FillAsync(string rankNameSlug, CertificateTemplateData data, CancellationToken ct = default)
    {
        var templatePath = ResolveTemplatePath(rankNameSlug);
        _logger.LogDebug("Using certificate template: {Path}", templatePath);

        // Run synchronous iText7 I/O on a thread-pool thread to keep async context clean
        return await Task.Run(() => FillTemplate(templatePath, data), ct);
    }

    private string ResolveTemplatePath(string rankNameSlug)
    {
        var specific = Path.Combine(_templatesFolder, $"{rankNameSlug}.pdf");
        if (File.Exists(specific)) return specific;

        var fallback = Path.Combine(_templatesFolder, "default.pdf");
        if (File.Exists(fallback)) return fallback;

        throw new FileNotFoundException(
            $"No certificate template found. Looked for '{specific}' and '{fallback}'.");
    }

    private static byte[] FillTemplate(string templatePath, CertificateTemplateData data)
    {
        using var output = new MemoryStream();

        using var reader = new PdfReader(templatePath);
        using var writer = new PdfWriter(output);
        using var pdfDoc = new PdfDocument(reader, writer);

        var form = PdfAcroForm.GetAcroForm(pdfDoc, createIfNotExist: false);

        if (form is null)
            throw new InvalidOperationException(
                $"The PDF template at '{templatePath}' has no AcroForm. " +
                "Add fillable fields named 'FullName' and 'AchievedDate' to the template.");

        SetField(form, FieldFullName,    data.FullName);
        SetField(form, FieldAchievedDate, data.AchievedAt.ToString("MMMM dd, yyyy"));

        // Flatten: converts interactive fields to static content (non-editable PDF)
        form.FlattenFields();

        pdfDoc.Close();
        return output.ToArray();
    }

    private static void SetField(PdfAcroForm form, string fieldName, string value)
    {
        var field = form.GetField(fieldName);
        if (field is null) return; // skip missing optional fields gracefully
        field.SetValue(value);
    }
}
