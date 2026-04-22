namespace MLMConquerorGlobalEdition.BizCenter.Services;

public interface IS3PresignedUrlService
{
    Task<string> GeneratePresignedDownloadUrlAsync(string s3Key, string fileName, int expiryMinutes = 15, CancellationToken ct = default);
}
