using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankDashboard;

public class GetRankDashboardHandler : IRequestHandler<GetRankDashboardQuery, Result<RankDashboardDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public GetRankDashboardHandler(AppDbContext db, ICurrentUserService currentUser, ICacheService cache)
    {
        _db          = db;
        _currentUser = currentUser;
        _cache       = cache;
    }

    public async Task<Result<RankDashboardDto>> Handle(GetRankDashboardQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var cacheKey = CacheKeys.MemberRank(memberId);

        var cached = await _cache.GetAsync<RankDashboardDto>(cacheKey, ct);
        if (cached is not null)
            return Result<RankDashboardDto>.Success(cached);

        // Load member stats
        var stats = await _db.MemberStatistics
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemberId == memberId, ct);

        // Current rank — most recently achieved
        var currentRankHistory = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.AchievedAt)
            .FirstOrDefaultAsync(ct);

        // Lifetime rank — highest SortOrder ever achieved
        var lifetimeRankHistory = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.RankDefinition!.SortOrder)
            .FirstOrDefaultAsync(ct);

        var dto = new RankDashboardDto
        {
            MemberId                   = memberId,
            CurrentRankName            = currentRankHistory?.RankDefinition?.Name,
            CurrentRankSortOrder       = currentRankHistory?.RankDefinition?.SortOrder ?? 0,
            LifetimeRankName           = lifetimeRankHistory?.RankDefinition?.Name,
            DualTeamPoints             = stats?.DualTeamPoints ?? 0,
            EnrollmentPoints           = stats?.EnrollmentPoints ?? 0,
            QualifiedSponsoredMembers  = stats?.QualifiedSponsoredMembers ?? 0,
            EnrollmentTeamSize         = stats?.EnrollmentTeamSize ?? 0
        };

        await _cache.SetAsync(cacheKey, dto, CacheKeys.MemberRankTtl, ct);

        return Result<RankDashboardDto>.Success(dto);
    }
}
