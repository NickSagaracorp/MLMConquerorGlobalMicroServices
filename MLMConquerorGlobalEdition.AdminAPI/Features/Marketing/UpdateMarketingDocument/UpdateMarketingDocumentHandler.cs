using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UpdateMarketingDocument;

public class UpdateMarketingDocumentHandler : IRequestHandler<UpdateMarketingDocumentCommand, Result<MarketingDocumentDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public UpdateMarketingDocumentHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db          = db;
        _currentUser = cu;
        _dateTime    = dt;
    }

    public async Task<Result<MarketingDocumentDto>> Handle(UpdateMarketingDocumentCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.MarketingDocuments
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result<MarketingDocumentDto>.Failure("NOT_FOUND", "Document not found.");

        var req = request.Request;

        if (req.DocumentTypeId != entity.DocumentTypeId)
        {
            var typeExists = await _db.DocumentTypes.AnyAsync(t => t.Id == req.DocumentTypeId, cancellationToken);
            if (!typeExists)
                return Result<MarketingDocumentDto>.Failure("NOT_FOUND", "Document type not found.");
        }

        entity.Title          = req.Title;
        entity.DocumentTypeId = req.DocumentTypeId;
        entity.LanguageCode   = req.LanguageCode;
        entity.LanguageName   = req.LanguageName;
        entity.IsActive       = req.IsActive;
        entity.LastUpdateDate = _dateTime.Now;
        entity.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<MarketingDocumentDto>.Success(new MarketingDocumentDto
        {
            Id               = entity.Id,
            Title            = entity.Title,
            DocumentTypeId   = entity.DocumentTypeId,
            DocumentTypeName = entity.DocumentType?.Name ?? string.Empty,
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
