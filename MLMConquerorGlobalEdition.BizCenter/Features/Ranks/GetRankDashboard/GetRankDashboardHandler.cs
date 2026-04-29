using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankDashboard;

/// <summary>
/// BizCenter rank dashboard endpoint. Delegates ALL rank computation to the
/// shared <see cref="IRankComputationService"/> — do not put rank logic here.
/// </summary>
public class GetRankDashboardHandler : IRequestHandler<GetRankDashboardQuery, Result<RankDashboardDto>>
{
    private readonly IRankComputationService _ranks;
    private readonly ICurrentUserService     _currentUser;
    private readonly ICacheService           _cache;

    public GetRankDashboardHandler(
        IRankComputationService ranks,
        ICurrentUserService currentUser,
        ICacheService cache)
    {
        _ranks       = ranks;
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

        var summary = await _ranks.GetSummaryAsync(memberId, ct);
        var dto     = MapToBizCenterDto(summary);

        await _cache.SetAsync(cacheKey, dto, CacheKeys.MemberRankTtl, ct);
        return Result<RankDashboardDto>.Success(dto);
    }

    private static RankDashboardDto MapToBizCenterDto(RankSummaryDto s) => new()
    {
        MemberId                    = s.MemberId,
        CurrentRankName             = s.CurrentRankName,
        CurrentRankSortOrder        = s.CurrentRankSortOrder,
        CurrentRankDualTeamPoints   = s.CurrentRankDualTeamPoints,
        CurrentRankEnrollmentPoints = s.CurrentRankEnrollmentPoints,
        NextRankName                = s.NextRankName,
        NextRankSortOrder           = s.NextRankSortOrder,
        NextRankDualTeamPoints      = s.NextRankDualTeamPoints,
        NextRankEnrollmentPoints    = s.NextRankEnrollmentPoints,
        LifetimeRankName            = s.LifetimeRankName,
        DualTeamPoints              = s.DualTeamPoints,
        EnrollmentPoints            = s.EnrollmentPoints,
        QualifiedSponsoredMembers   = s.QualifiedSponsoredMembers,
        EnrollmentTeamSize          = s.EnrollmentTeamSize
    };
}
