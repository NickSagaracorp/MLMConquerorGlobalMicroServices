using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

public class S3PresignedUrlService : IS3PresignedUrlService
{
    private readonly AppDbContext    _db;
    private readonly IConfiguration _config;

    public S3PresignedUrlService(AppDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    public async Task<string> GeneratePresignedDownloadUrlAsync(
        string s3Key,
        string fileName,
        int    expiryMinutes = 15,
        CancellationToken ct = default)
    {
        var cfg = await _db.S3StorageConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, ct)
            ?? throw new InvalidOperationException("S3 bucket configuration has not been set.");

        var region = RegionEndpoint.GetBySystemName(cfg.Region);
        using var client = BuildClient(region);

        var request = new GetPreSignedUrlRequest
        {
            BucketName  = cfg.BucketName,
            Key         = s3Key,
            Expires     = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Verb        = HttpVerb.GET,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{fileName}\""
            }
        };

        return client.GetPreSignedURL(request);
    }

    private AmazonS3Client BuildClient(RegionEndpoint region)
    {
        var accessKey = _config["AWS:AccessKey"];
        var secretKey = _config["AWS:SecretKey"];

        return !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey)
            ? new AmazonS3Client(accessKey, secretKey, region)
            : new AmazonS3Client(region);
    }
}
