using Amazon.S3;
using Amazon.S3.Model;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public class S3FileService : IS3FileService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly string _bucketRegion;

    private readonly bool _isConfigured;

    public S3FileService(IAmazonS3 s3, IConfiguration configuration)
    {
        _s3 = s3;
        _bucketName   = configuration["AWS:S3:BucketName"] ?? string.Empty;
        _bucketRegion = configuration["AWS:S3:Region"]     ?? "us-east-1";
        _isConfigured = !string.IsNullOrEmpty(_bucketName);
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        if (!_isConfigured)
            return string.Empty;   // S3 not wired — skip upload in dev/local environments

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
