using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.GetCorporateEvents;

public class GetCorporateEventsHandler : IRequestHandler<GetCorporateEventsQuery, Result<PagedResult<CorporateEventDto>>>
{
    private readonly AppDbContext _db;

    public GetCorporateEventsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<CorporateEventDto>>> Handle(
        GetCorporateEventsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.CorporateEvents.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.EventDate)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .Select(e => new CorporateEventDto
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                EventDate = e.EventDate,
                Location = e.Location,
                ImageUrl = e.ImageUrl,
                IsActive = e.IsActive,
                CreationDate = e.CreationDate
            })
            .ToListAsync(cancellationToken);

        return Result<PagedResult<CorporateEventDto>>.Success(new PagedResult<CorporateEventDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        });
    }
}
