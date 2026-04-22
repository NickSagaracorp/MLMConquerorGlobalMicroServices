using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.GetMarketingDocuments;

public class GetMarketingDocumentsHandler : IRequestHandler<GetMarketingDocumentsQuery, Result<List<MarketingDocumentDto>>>
{
    private readonly AppDbContext _db;

    public GetMarketingDocumentsHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<MarketingDocumentDto>>> Handle(GetMarketingDocumentsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.MarketingDocuments
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .AsQueryable();

        if (request.DocumentTypeId.HasValue)
            query = query.Where(d => d.DocumentTypeId == request.DocumentTypeId.Value);

        if (request.IsActive.HasValue)
            query = query.Where(d => d.IsActive == request.IsActive.Value);

        var docs = await query
            .OrderBy(d => d.DocumentTypeId)
            .ThenBy(d => d.Title)
            .Select(d => new MarketingDocumentDto
            {
                Id               = d.Id,
                Title            = d.Title,
                DocumentTypeId   = d.DocumentTypeId,
                DocumentTypeName = d.DocumentType != null ? d.DocumentType.Name : string.Empty,
                LanguageCode     = d.LanguageCode,
                LanguageName     = d.LanguageName,
                OriginalFileName = d.OriginalFileName,
                FileSizeBytes    = d.FileSizeBytes,
                ContentType      = d.ContentType,
                IsActive         = d.IsActive,
                CreationDate     = d.CreationDate
            })
            .ToListAsync(cancellationToken);

        return Result<List<MarketingDocumentDto>>.Success(docs);
    }
}
