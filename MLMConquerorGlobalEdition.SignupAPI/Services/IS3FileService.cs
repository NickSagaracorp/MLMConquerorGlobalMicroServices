namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public interface IS3FileService
{
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    string GetPublicUrl(string key);
}
