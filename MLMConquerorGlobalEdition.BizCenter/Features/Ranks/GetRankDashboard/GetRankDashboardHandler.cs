using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankDashboard;

public class GetRankDashboardHandler : IRequestHandler<GetRankDashboardQuery, Result<RankDashboardDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRankDashboardHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<RankDashboardDto>> Handle(GetRankDashboardQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Load member stats
        var stats = await _db.MemberStatistics
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemberId == memberId, ct);

        // Load most recent rank (current rank = last achieved)
        var currentRankHistory = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.AchievedAt)
            .FirstOrDefaultAsync(ct);

        // Load highest rank achieved (lifetime rank = highest SortOrder)
        var lifetimeRankHistory = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.RankDefinition!.SortOrder)
            .FirstOrDefaultAsync(ct);

        var dto = new RankDashboardDto
        {
            MemberId = memberId,
            CurrentRankName = currentRankHistory?.RankDefinition?.Name,
            CurrentRankSortOrder = currentRankHistory?.RankDefinition?.SortOrder ?? 0,
            LifetimeRankName = lifetimeRankHistory?.RankDefinition?.Name,
            DualTeamPoints = stats?.DualTeamPoints ?? 0,
            EnrollmentPoints = stats?.EnrollmentPoints ?? 0,
            QualifiedSponsoredMembers = stats?.QualifiedSponsoredMembers ?? 0,
            EnrollmentTeamSize = stats?.EnrollmentTeamSize ?? 0
        };

        return Result<RankDashboardDto>.Success(dto);
    }
}
