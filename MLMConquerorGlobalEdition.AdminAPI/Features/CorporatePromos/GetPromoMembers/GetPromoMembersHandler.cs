using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoMembers;

public class GetPromoMembersHandler
    : IRequestHandler<GetPromoMembersQuery, Result<PagedResult<PromoMemberDto>>>
{
    private readonly AppDbContext _db;

    public GetPromoMembersHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<PromoMemberDto>>> Handle(
        GetPromoMembersQuery request, CancellationToken cancellationToken)
    {
        var promo = await _db.CorporatePromos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PromoId && !x.IsDeleted, cancellationToken);

        if (promo is null)
            return Result<PagedResult<PromoMemberDto>>.Failure("PROMO_NOT_FOUND", "Corporate promo not found.");

        var subsQuery = _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => s.CreationDate >= promo.StartDate && s.CreationDate <= promo.EndDate)
            .GroupBy(s => s.MemberId)
            .Select(g => new
            {
                MemberId = g.Key,
                SignupDate = g.Min(s => s.CreationDate)
            });

        var totalCount = await subsQuery.CountAsync(cancellationToken);

        var subItems = await subsQuery
            .OrderBy(x => x.SignupDate)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);

        var memberIds = subItems.Select(x => x.MemberId).ToList();

        var members = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => memberIds.Contains(m.MemberId))
            .Select(m => new
            {
                m.MemberId,
                m.FirstName,
                m.LastName
            })
            .ToListAsync(cancellationToken);

        var activeSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => memberIds.Contains(s.MemberId))
            .Include(s => s.MembershipLevel)
            .GroupBy(s => s.MemberId)
            .Select(g => new
            {
                MemberId = g.Key,
                LevelName = g.OrderByDescending(s => s.CreationDate)
                              .Select(s => s.MembershipLevel != null ? s.MembershipLevel.Name : string.Empty)
                              .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var levelLookup = activeSubs.ToDictionary(x => x.MemberId, x => x.LevelName);
        var memberLookup = members.ToDictionary(x => x.MemberId);

        var items = subItems.Select(s =>
        {
            memberLookup.TryGetValue(s.MemberId, out var m);
            levelLookup.TryGetValue(s.MemberId, out var level);
            return new PromoMemberDto
            {
                MemberId = s.MemberId,
                FullName = m is not null ? $"{m.FirstName} {m.LastName}".Trim() : s.MemberId,
                MembershipLevel = level ?? string.Empty,
                SignupDate = s.SignupDate
            };
        }).ToList();

        return Result<PagedResult<PromoMemberDto>>.Success(new PagedResult<PromoMemberDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        });
    }
}
