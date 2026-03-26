namespace MLMConquerorGlobalEdition.RankEngine.Services;

public interface ICertificatePdfFillerService
{
    /// <summary>
    /// Loads the PDF template for the given rank slug, fills the AcroForm fields
    /// with member data, flattens the form, and returns the resulting PDF bytes.
    /// Falls back to "default.pdf" if no rank-specific template exists.
    /// </summary>
    Task<byte[]> FillAsync(string rankNameSlug, CertificateTemplateData data, CancellationToken ct = default);
}
