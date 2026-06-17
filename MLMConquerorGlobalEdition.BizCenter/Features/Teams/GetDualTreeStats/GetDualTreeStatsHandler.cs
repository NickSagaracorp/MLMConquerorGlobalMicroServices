using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTreeStats;

/// <summary>
/// Left / Right leg point totals for a member's binary-tree position. Thin wrapper over the
/// shared <see cref="IDualTeamService.GetDualTreeStatsAsync"/> — the SAME method the Admin
/// dual-tree/stats endpoint uses, so the two surfaces can never drift. The leg totals come from
/// the denormalised <c>DualTeamTree.LeftLegPoints/RightLegPoints</c> columns (maintained by the
/// placement engine, shared with rank qualification) — O(1), not an O(downline) subtree
/// recompute. Cached for 2 minutes because the visualizer and residuals page hit it per render.
/// </summary>
public class GetDualTreeStatsHandler : IRequestHandler<GetDualTreeStatsQuery, Result<DualTreeStatsDto>>
{
    private readonly IDualTeamService _dualTeam;
    private readonly ICacheService    _cache;

    public GetDualTreeStatsHandler(IDualTeamService dualTeam, ICacheService cache)
    {
        _dualTeam = dualTeam;
        _cache    = cache;
    }

    public async Task<Result<DualTreeStatsDto>> Handle(GetDualTreeStatsQuery request, CancellationToken ct)
    {
        var memberId = request.NodeMemberId;
        var cacheKey = CacheKeys.DualTreeStats(memberId);

        var cached = await _cache.GetAsync<DualTreeStatsDto>(cacheKey, ct);
        if (cached is not null)
            return Result<DualTreeStatsDto>.Success(cached);

        var stats = await _dualTeam.GetDualTreeStatsAsync(memberId, ct);
        var dto   = new DualTreeStatsDto
        {
            LeftLegPoints  = stats.LeftLegPoints,
            RightLegPoints = stats.RightLegPoints
        };

        await _cache.SetAsync(cacheKey, dto, CacheKeys.DualTreeStatsTtl, ct);
        return Result<DualTreeStatsDto>.Success(dto);
    }
}
