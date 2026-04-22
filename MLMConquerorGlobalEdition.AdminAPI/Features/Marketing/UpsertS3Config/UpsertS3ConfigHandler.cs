using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.Domain.Entities.Marketing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UpsertS3Config;

public class UpsertS3ConfigHandler : IRequestHandler<UpsertS3ConfigCommand, Result<S3StorageConfigDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public UpsertS3ConfigHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db          = db;
        _currentUser = cu;
        _dateTime    = dt;
    }

    public async Task<Result<S3StorageConfigDto>> Handle(UpsertS3ConfigCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var now = _dateTime.Now;

        var existing = await _db.S3StorageConfigs.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (existing is null)
        {
            var entity = new S3StorageConfig
            {
                Id           = 1,
                BucketName   = req.BucketName,
                Region       = req.Region,
                FolderPrefix = req.FolderPrefix,
                CreationDate = now,
                CreatedBy    = _currentUser.UserId,
                LastUpdateDate = now,
                LastUpdateBy = _currentUser.UserId
            };
            await _db.S3StorageConfigs.AddAsync(entity, cancellationToken);
        }
        else
        {
            existing.BucketName    = req.BucketName;
            existing.Region        = req.Region;
            existing.FolderPrefix  = req.FolderPrefix;
            existing.LastUpdateDate = now;
            existing.LastUpdateBy  = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<S3StorageConfigDto>.Success(new S3StorageConfigDto
        {
            BucketName   = req.BucketName,
            Region       = req.Region,
            FolderPrefix = req.FolderPrefix
        });
    }
}
