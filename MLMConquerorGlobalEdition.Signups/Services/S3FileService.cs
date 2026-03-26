using Amazon.S3;
using Amazon.S3.Model;

namespace MLMConquerorGlobalEdition.Signups.Services;

public class S3FileService : IS3FileService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly string _bucketRegion;

    public S3FileService(IAmazonS3 s3, IConfiguration configuration)
    {
        _s3 = s3;
        _bucketName = configuration["AWS:S3:BucketName"]
            ?? throw new InvalidOperationException("AWS:S3:BucketName is not configured.");
        _bucketRegion = configuration["AWS:S3:Region"]
            ?? throw new InvalidOperationException("AWS:S3:Region is not configured.");
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _s3.PutObjectAsync(request, ct);
        return GetPublicUrl(key);
    }

    public string GetPublicUrl(string key)
        => $"https://{_bucketName}.s3.{_bucketRegion}.amazonaws.com/{key}";
}
