namespace MLMConquerorGlobalEdition.RankEngine.Services;

public interface IS3FileService
{
    /// <summary>
    /// Uploads a file stream to S3 and returns the public URL.
    /// </summary>
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Returns the public URL for an existing S3 object key.
    /// </summary>
    string GetPublicUrl(string key);

    /// <summary>
    /// Generates a pre-signed URL for temporary access to a private S3 object.
    /// </summary>
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}
