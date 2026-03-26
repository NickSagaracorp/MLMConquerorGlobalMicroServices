using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetCorporatePromos;

public class GetCorporatePromosHandler
    : IRequestHandler<GetCorporatePromosQuery, Result<PagedResult<CorporatePromoDto>>>
{
    private readonly AppDbContext _db;

    public GetCorporatePromosHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<CorporatePromoDto>>> Handle(
        GetCorporatePromosQuery request, CancellationToken cancellationToken)
    {
        var query = _db.CorporatePromos.AsNoTracking().Where(x => !x.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);

        var promos = await query
            .OrderByDescending(x => x.StartDate)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<CorporatePromoDto>();
        foreach (var promo in promos)
        {
            var memberCount = await _db.MembershipSubscriptions
                .AsNoTracking()
                .Where(s => s.CreationDate >= promo.StartDate && s.CreationDate <= promo.EndDate)
                .Select(s => s.MemberId)
                .Distinct()
                .CountAsync(cancellationToken);

            items.Add(new CorporatePromoDto
            {
                Id = promo.Id,
                Title = promo.Title,
                Description = promo.Description,
                StartDate = promo.StartDate,
                EndDate = promo.EndDate,
                BannerUrl = promo.BannerUrl,
                IsActive = promo.IsActive,
                MemberCount = memberCount,
                CreationDate = promo.CreationDate
            });
        }

        return Result<PagedResult<CorporatePromoDto>>.Success(new PagedResult<CorporatePromoDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        });
    }
}
