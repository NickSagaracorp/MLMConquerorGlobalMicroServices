namespace MLMConquerorGlobalEdition.RankEngine.Services;

/// <summary>
/// Persists generated certificate PDFs. Implemented by LocalCertificateStorage now;
/// an S3CertificateStorage can be swapped in later via configuration.
/// </summary>
public interface ICertificateStorage
{
    /// <summary>Saves the PDF under fileName (overwriting any existing file) and returns its public URL.</summary>
    Task<string> SaveAsync(string fileName, byte[] content, CancellationToken ct = default);

    /// <summary>Removes the stored file. No-op when the file does not exist.</summary>
    Task DeleteAsync(string fileName, CancellationToken ct = default);
}
