namespace MLMConquerorGlobalEdition.RankEngine.Services;

public interface ICertificatePdfFillerService
{
    /// <summary>
    /// Loads the PDF template for the given rank sort-order number (e.g. 1.pdf, 2.pdf…),
    /// fills the two AcroForm fields (FullName, AchievedDate), flattens the form,
    /// and returns the resulting PDF bytes.
    /// </summary>
    Task<byte[]> FillAsync(int rankSortOrder, CertificateTemplateData data, CancellationToken ct = default);
}
