using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTreeStats;

/// <summary>
/// Returns aggregated Left / Right leg point totals for a member's binary-tree
/// position. Computes the totals on the fly from the canonical source
/// (<see cref="MLMConquerorGlobalEdition.Domain.Entities.Member.MemberStatisticEntity"/>)
/// summed per subtree, instead of trusting the denormalised
/// <c>DualTeamEntity.LeftLegPoints</c>/<c>RightLegPoints</c> columns which
/// can drift when downline orders / placements happen.
///
/// Cached for 2 minutes per member because the visualizer and the residuals
/// page hit this on every render, but actual leg totals only move when a
/// downline order completes.
/// </summary>
public class GetDualTreeStatsHandler : IRequestHandler<GetDualTreeStatsQuery, Result<DualTreeStatsDto>>
{
    private readonly AppDbContext  _db;
    private readonly ICacheService _cache;

    public GetDualTreeStatsHandler(AppDbContext db, ICacheService cache)
    {
        _db    = db;
        _cache = cache;
    }

    public async Task<Result<DualTreeStatsDto>> Handle(GetDualTreeStatsQuery request, CancellationToken ct)
    {
        var memberId = request.NodeMemberId;
        var cacheKey = CacheKeys.DualTreeStats(memberId);

        var cached = await _cache.GetAsync<DualTreeStatsDto>(cacheKey, ct);
        if (cached is not null)
            return Result<DualTreeStatsDto>.Success(cached);

        var node = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
        if (node is null)
            return Result<DualTreeStatsDto>.Success(new DualTreeStatsDto());

        // ─── Direct binary children — their Side determines which leg every
        //      deeper descendant lands on (the gateway-node Side).
        var directChildren = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == memberId)
            .Select(d => new { d.MemberId, d.Side })
            .ToListAsync(ct);
        var legByGatewayId = directChildren.ToDictionary(d => d.MemberId, d => d.Side);

        // ─── Pull the entire subtree (excluding self) so we can sum points.
        var pathPrefix   = node.HierarchyPath;
        var subtreeNodes = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.HierarchyPath.StartsWith(pathPrefix) && d.MemberId != memberId)
            .Select(d => new { d.MemberId, d.HierarchyPath })
            .ToListAsync(ct);

        if (subtreeNodes.Count == 0)
        {
            var emptyDto = new DualTreeStatsDto();
            await _cache.SetAsync(cacheKey, emptyDto, CacheKeys.DualTreeStatsTtl, ct);
            return Result<DualTreeStatsDto>.Success(emptyDto);
        }

        // Map each subtree member to the leg they belong to (Left / Right /
        // empty for orphaned paths). The first segment of the path after
        // pathPrefix is the gateway-node id; we look it up in legByGatewayId.
        var legByMemberId = new Dictionary<string, string>(subtreeNodes.Count);
        foreach (var n in subtreeNodes)
        {
            var rel      = n.HierarchyPath.Length > pathPrefix.Length
                ? n.HierarchyPath[pathPrefix.Length..]
                : string.Empty;
            var firstSeg = rel.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (firstSeg is not null && legByGatewayId.TryGetValue(firstSeg, out var side))
                legByMemberId[n.MemberId] = side.ToString();
        }

        var subtreeIds = subtreeNodes.Select(n => n.MemberId).ToList();

        // PersonalPoints is the per-member contribution; sum per leg.
        var pointsByMember = await _db.MemberStatistics.AsNoTracking()
            .Where(s => subtreeIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.PersonalPoints })
            .ToListAsync(ct);

        decimal leftLegPoints  = 0;
        decimal rightLegPoints = 0;
        foreach (var p in pointsByMember)
        {
            if (!legByMemberId.TryGetValue(p.MemberId, out var leg)) continue;
            if (leg == "Left")  leftLegPoints  += p.PersonalPoints;
            else if (leg == "Right") rightLegPoints += p.PersonalPoints;
        }

        var dto = new DualTreeStatsDto
        {
            LeftLegPoints  = leftLegPoints,
            RightLegPoints = rightLegPoints
        };

        await _cache.SetAsync(cacheKey, dto, CacheKeys.DualTreeStatsTtl, ct);
        return Result<DualTreeStatsDto>.Success(dto);
    }
}
