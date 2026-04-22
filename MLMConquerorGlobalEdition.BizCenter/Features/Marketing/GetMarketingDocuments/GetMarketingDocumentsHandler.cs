using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Marketing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Marketing.GetMarketingDocuments;

public class GetMarketingDocumentsHandler : IRequestHandler<GetMarketingDocumentsQuery, Result<List<MarketingDocumentSummaryDto>>>
{
    private readonly AppDbContext _db;

    public GetMarketingDocumentsHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<MarketingDocumentSummaryDto>>> Handle(GetMarketingDocumentsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.MarketingDocuments
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .Where(d => d.IsActive);

        if (request.DocumentTypeId.HasValue)
            query = query.Where(d => d.DocumentTypeId == request.DocumentTypeId.Value);

        var docs = await query
            .OrderBy(d => d.DocumentTypeId)
            .ThenBy(d => d.Title)
            .Select(d => new MarketingDocumentSummaryDto
            {
                Id               = d.Id,
                Title            = d.Title,
                DocumentTypeName = d.DocumentType != null ? d.DocumentType.Name : string.Empty,
                LanguageCode     = d.LanguageCode,
                LanguageName     = d.LanguageName,
                OriginalFileName = d.OriginalFileName,
                FileSizeBytes    = d.FileSizeBytes,
                ContentType      = d.ContentType
            })
            .ToListAsync(cancellationToken);

        return Result<List<MarketingDocumentSummaryDto>>.Success(docs);
    }
}
