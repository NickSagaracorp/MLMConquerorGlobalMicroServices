using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetVisualizerChildren;

public class GetVisualizerChildrenHandler
    : IRequestHandler<GetVisualizerChildrenQuery, Result<IEnumerable<EnrollmentVisualizerNodeDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisualizerChildrenHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<IEnumerable<EnrollmentVisualizerNodeDto>>> Handle(
        GetVisualizerChildrenQuery request, CancellationToken ct)
    {
        var currentMemberId = _currentUser.MemberId;

        // ── Security: parentMemberId must be the current user OR exist within their subtree ──
        // The root case: current member themselves is always allowed.
        if (request.ParentMemberId != currentMemberId)
        {
            var pattern = "/" + currentMemberId + "/";
            var isInSubtree = await _db.GenealogyTree
                .AsNoTracking()
                .AnyAsync(g => g.MemberId == request.ParentMemberId
                            && g.HierarchyPath.Contains(pattern), ct);

            if (!isInSubtree)
                return Result<IEnumerable<EnrollmentVisualizerNodeDto>>
                    .Failure("VISUALIZER_ACCESS_DENIED",
                             "The requested member is not within your enrollment subtree.");
        }

        // ── 1. Get direct children of the requested parent ─────────────────────────
        var childIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.ParentMemberId == request.ParentMemberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (!childIds.Any())
            return Result<IEnumerable<EnrollmentVisualizerNodeDto>>
                .Success(Enumerable.Empty<EnrollmentVisualizerNodeDto>());

        // ── 2. Fetch member profiles for all children ──────────────────────────────
        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => childIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, m.FirstName, m.LastName, m.Status })
            .ToListAsync(ct);

        // ── 3. Fetch enrollment points from MemberStatistics ───────────────────────
        var stats = await _db.MemberStatistics
            .AsNoTracking()
            .Where(s => childIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);

        var statsMap = stats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);

        // ── 4. Determine which children have grandchildren ─────────────────────────
        var grandChildParentIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => childIds.Contains(g.ParentMemberId))
            .Select(g => g.ParentMemberId)
            .Distinct()
            .ToListAsync(ct);

        var hasChildrenSet = new HashSet<string>(grandChildParentIds);

        // ── 5. Build result nodes ─────────────────────────────────────────────────
        var nodes = profiles
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .Select(p => new EnrollmentVisualizerNodeDto
            {
                MemberId    = p.MemberId,
                FullName    = $"{p.FirstName} {p.LastName}".Trim(),
                StatusCode  = p.Status switch
                {
                    MemberAccountStatus.Active                                        => "Q",
                    MemberAccountStatus.Inactive or MemberAccountStatus.Suspended    => "U",
                    _                                                                 => "C"
                },
                Points      = statsMap.TryGetValue(p.MemberId, out var pts) ? pts : 0,
                HasChildren = hasChildrenSet.Contains(p.MemberId)
            })
            .ToList();

        return Result<IEnumerable<EnrollmentVisualizerNodeDto>>.Success(nodes);
    }
}
