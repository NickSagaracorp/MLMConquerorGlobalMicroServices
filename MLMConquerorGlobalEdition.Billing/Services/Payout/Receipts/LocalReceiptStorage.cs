namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public class LocalReceiptStorage : IReceiptStorage
{
    private const string UrlSegment = "payout-receipts";
    private readonly string _folderPath;
    private readonly string _publicBaseUrl;

    public LocalReceiptStorage(string folderPath, string publicBaseUrl)
    {
        _folderPath = folderPath;
        _publicBaseUrl = publicBaseUrl.TrimEnd('/');
    }

    public async Task<string> SaveAsync(string fileName, byte[] content, CancellationToken ct = default)
    {
        var safe = Path.GetFileName(fileName); // path-traversal defense
        Directory.CreateDirectory(_folderPath);
        await File.WriteAllBytesAsync(Path.Combine(_folderPath, safe), content, ct);
        return $"{_publicBaseUrl}/{UrlSegment}/{safe}";
    }

    public async Task<byte[]?> ReadAsync(string fileName, CancellationToken ct = default)
    {
        var safe = Path.GetFileName(fileName);
        var full = Path.Combine(_folderPath, safe);
        return File.Exists(full) ? await File.ReadAllBytesAsync(full, ct) : null;
    }
}
