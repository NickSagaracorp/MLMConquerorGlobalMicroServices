using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankHistory;

public class GetRankHistoryHandler : IRequestHandler<GetRankHistoryQuery, Result<PagedResult<RankHistoryDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRankHistoryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<RankHistoryDto>>> Handle(GetRankHistoryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var query = _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.AchievedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new RankHistoryDto
            {
                Id = r.Id,
                RankName = r.RankDefinition != null ? r.RankDefinition.Name : string.Empty,
                AchievedAt = r.AchievedAt,
                PreviousRankId = r.PreviousRankId,
                CertificateUrl = r.GeneratedCertificateUrl
            })
            .ToListAsync(ct);

        var result = new PagedResult<RankHistoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<RankHistoryDto>>.Success(result);
    }
}
