using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.GetDocumentTypes;

public class GetDocumentTypesHandler : IRequestHandler<GetDocumentTypesQuery, Result<List<DocumentTypeDto>>>
{
    private readonly AppDbContext _db;

    public GetDocumentTypesHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<DocumentTypeDto>>> Handle(GetDocumentTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await _db.DocumentTypes
            .AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .Select(t => new DocumentTypeDto
            {
                Id          = t.Id,
                Name        = t.Name,
                Description = t.Description,
                SortOrder   = t.SortOrder,
                IsActive    = t.IsActive
            })
            .ToListAsync(cancellationToken);

        return Result<List<DocumentTypeDto>>.Success(types);
    }
}
