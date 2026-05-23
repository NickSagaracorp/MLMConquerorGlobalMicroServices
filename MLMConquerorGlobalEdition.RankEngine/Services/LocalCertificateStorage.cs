namespace MLMConquerorGlobalEdition.RankEngine.Services;

/// <summary>
/// Stores certificate PDFs on the local filesystem under a folder served as static
/// content (wwwroot/certificates). Used until S3 credentials are available.
/// </summary>
public class LocalCertificateStorage : ICertificateStorage
{
    private const string UrlSegment = "certificates";

    private readonly string _folderPath;
    private readonly string _publicBaseUrl;

    public LocalCertificateStorage(string folderPath, string publicBaseUrl)
    {
        _folderPath    = folderPath;
        _publicBaseUrl = publicBaseUrl.TrimEnd('/');
    }

    public async Task<string> SaveAsync(string fileName, byte[] content, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);   // defends against path traversal
        Directory.CreateDirectory(_folderPath);
        await File.WriteAllBytesAsync(Path.Combine(_folderPath, safeName), content, ct);
        return $"{_publicBaseUrl}/{UrlSegment}/{safeName}";
    }

    public Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(_folderPath, safeName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
