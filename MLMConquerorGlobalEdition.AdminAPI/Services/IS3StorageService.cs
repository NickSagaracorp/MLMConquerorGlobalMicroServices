namespace MLMConquerorGlobalEdition.AdminAPI.Services;

public interface IS3StorageService
{
    Task<string> UploadAsync(Stream fileStream, string s3Key, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string s3Key, CancellationToken ct = default);
}
