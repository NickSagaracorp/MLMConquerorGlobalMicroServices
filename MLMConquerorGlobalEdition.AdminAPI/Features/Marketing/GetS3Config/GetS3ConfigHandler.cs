using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.GetS3Config;

public class GetS3ConfigHandler : IRequestHandler<GetS3ConfigQuery, Result<S3StorageConfigDto>>
{
    private readonly AppDbContext _db;

    public GetS3ConfigHandler(AppDbContext db) => _db = db;

    public async Task<Result<S3StorageConfigDto>> Handle(GetS3ConfigQuery request, CancellationToken cancellationToken)
    {
        var cfg = await _db.S3StorageConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (cfg is null)
            return Result<S3StorageConfigDto>.Success(new S3StorageConfigDto());

        return Result<S3StorageConfigDto>.Success(new S3StorageConfigDto
        {
            BucketName   = cfg.BucketName,
            Region       = cfg.Region,
            FolderPrefix = cfg.FolderPrefix
        });
    }
}
