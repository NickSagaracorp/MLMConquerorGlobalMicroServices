using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.AdminAPI.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Marketing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UploadMarketingDocument;

public class UploadMarketingDocumentHandler : IRequestHandler<UploadMarketingDocumentCommand, Result<MarketingDocumentDto>>
{
    private readonly AppDbContext        _db;
    private readonly IS3StorageService   _s3;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public UploadMarketingDocumentHandler(AppDbContext db, IS3StorageService s3, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db          = db;
        _s3          = s3;
        _currentUser = cu;
        _dateTime    = dt;
    }

    public async Task<Result<MarketingDocumentDto>> Handle(UploadMarketingDocumentCommand request, CancellationToken cancellationToken)
    {
        var typeExists = await _db.DocumentTypes.AnyAsync(t => t.Id == request.DocumentTypeId, cancellationToken);
        if (!typeExists)
            return Result<MarketingDocumentDto>.Failure("NOT_FOUND", "Document type not found.");

        var ext    = Path.GetExtension(request.OriginalFileName);
        var s3Key  = $"marketing-docs/{Guid.NewGuid()}{ext}";
        var now    = _dateTime.Now;

        string fullKey;
        try
        {
            fullKey = await _s3.UploadAsync(request.FileStream, s3Key, request.ContentType, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<MarketingDocumentDto>.Failure("S3_UPLOAD_FAILED", $"Upload failed: {ex.Message}");
        }

        var entity = new MarketingDocument
        {
            Title            = request.Title,
            DocumentTypeId   = request.DocumentTypeId,
            LanguageCode     = request.LanguageCode,
            LanguageName     = request.LanguageName,
            S3Key            = fullKey,
            OriginalFileName = request.OriginalFileName,
            FileSizeBytes    = request.FileSizeBytes,
            ContentType      = request.ContentType,
            IsActive         = true,
            CreationDate     = now,
            CreatedBy        = _currentUser.UserId,
            LastUpdateDate   = now,
            LastUpdateBy     = _currentUser.UserId
        };

        await _db.MarketingDocuments.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var typeName = (await _db.DocumentTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.DocumentTypeId, cancellationToken))?.Name ?? string.Empty;

        return Result<MarketingDocumentDto>.Success(new MarketingDocumentDto
        {
            Id               = entity.Id,
            Title            = entity.Title,
            DocumentTypeId   = entity.DocumentTypeId,
            DocumentTypeName = typeName,
            LanguageCode     = entity.LanguageCode,
            LanguageName     = entity.LanguageName,
            OriginalFileName = entity.OriginalFileName,
            FileSizeBytes    = entity.FileSizeBytes,
            ContentType      = entity.ContentType,
            IsActive         = entity.IsActive,
            CreationDate     = entity.CreationDate
        });
    }
}
