using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.AdminAPI.Services;

public class S3StorageService : IS3StorageService
{
    private readonly AppDbContext    _db;
    private readonly IConfiguration _config;

    public S3StorageService(AppDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    public async Task<string> UploadAsync(Stream fileStream, string s3Key, string contentType, CancellationToken ct = default)
    {
        var (client, bucket, fullKey) = await BuildClientAsync(s3Key, ct);

        var request = new PutObjectRequest
        {
            BucketName  = bucket,
            Key         = fullKey,
            InputStream = fileStream,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await client.PutObjectAsync(request, ct);
        return fullKey;
    }

    public async Task DeleteAsync(string s3Key, CancellationToken ct = default)
    {
        var (client, bucket, fullKey) = await BuildClientAsync(s3Key, ct);
        await client.DeleteObjectAsync(bucket, fullKey, ct);
    }

    private async Task<(AmazonS3Client client, string bucket, string fullKey)> BuildClientAsync(string key, CancellationToken ct)
    {
        var cfg = await _db.S3StorageConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, ct)
            ?? throw new InvalidOperationException("S3 bucket configuration has not been set. Configure it in Admin → S3 Config.");

        var region = RegionEndpoint.GetBySystemName(cfg.Region);
        var client = BuildClient(region);
        var prefix = cfg.FolderPrefix?.TrimEnd('/');
        var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}/{key}";

        return (client, cfg.BucketName, fullKey);
    }

    private AmazonS3Client BuildClient(RegionEndpoint region)
    {
        var accessKey = _config["AWS:AccessKey"];
        var secretKey = _config["AWS:SecretKey"];

        return !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey)
            ? new AmazonS3Client(accessKey, secretKey, region)
            : new AmazonS3Client(region);  // falls back to IAM role / env vars / credential chain
    }
}
