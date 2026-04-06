using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// HangFire recurring job — runs every 6 hours.
/// Auto-places ambassadors who have not received a manual placement
/// and whose 30-day placement window has elapsed.
///
/// Logic (placement-rules.md §5):
///   5.2.a  Sponsor has no children          → place on LEFT
///   5.2.b  Sponsor has only left child      → place on RIGHT
///   5.2.c  Sponsor has both children        → place on deepest available node
///          on the same side as the sponsor's position in their own upline.
///
/// Ghost points are NOT transferred on placement.
/// </summary>
public class AutoPlacementJob
{
    private const int PlacementWindowDays = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoPlacementJob> _logger;

    public AutoPlacementJob(
        IServiceScopeFactory      scopeFactory,
        ILogger<AutoPlacementJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public async Task ExecuteAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now   = clock.Now;

        // Members whose placement window has expired and are still unplaced
        var windowCutoff = now.AddDays(-PlacementWindowDays);

        var unplaced = await db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted
                     && m.EnrollDate <= windowCutoff
                     && m.SponsorMemberId != null)
            .ToListAsync();

        if (!unplaced.Any())
        {
            _logger.LogInformation("AutoPlacementJob: no unplaced members found at {Now}", now);
            return;
        }

        // Exclude members already in the dual tree
        var alreadyPlaced = await db.DualTeamTree
            .AsNoTracking()
            .Select(d => d.MemberId)
            .ToListAsync();

        var alreadyPlacedSet = new HashSet<string>(alreadyPlaced);

        var toAutoPlace = unplaced
            .Where(m => !alreadyPlacedSet.Contains(m.MemberId))
            .ToList();

        if (!toAutoPlace.Any())
        {
            _logger.LogInformation("AutoPlacementJob: all expired-window members are already placed.");
            return;
        }

        foreach (var member in toAutoPlace)
        {
            try
            {
                await AutoPlaceAsync(db, member.MemberId, member.SponsorMemberId!, now);
                _logger.LogInformation(
                    "AutoPlacementJob: auto-placed {MemberId} under sponsor {SponsorId}",
                    member.MemberId, member.SponsorMemberId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AutoPlacementJob: failed to auto-place {MemberId}", member.MemberId);
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("AutoPlacementJob completed at {Now}. Processed: {Count}",
            now, toAutoPlace.Count);
    }

    // ── Core auto-placement logic ────────────────────────────────────────────────

    private async Task AutoPlaceAsync(
        AppDbContext db, string memberId, string sponsorMemberId, DateTime now)
    {
        // Get sponsor's dual-team node (sponsor might not be in the tree yet either)
        var sponsorNode = await db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == sponsorMemberId);

        string targetParentId;
        TreeSide targetSide;

        if (sponsorNode is null)
        {
            // Sponsor has no tree position → create sponsor node at root first,
            // then place member as left child.
            // (This is an edge case; in practice sponsor should already be placed.)
            targetParentId = sponsorMemberId;
            targetSide     = TreeSide.Left;

            // Ensure sponsor root node exists
            if (!await db.DualTeamTree.AnyAsync(d => d.MemberId == sponsorMemberId))
            {
                db.DualTeamTree.Add(new DualTeamEntity
                {
                    MemberId      = sponsorMemberId,
                    ParentMemberId = null,
                    Side           = TreeSide.Left,
                    HierarchyPath  = $"/{sponsorMemberId}/",
                    CreationDate   = now,
                    CreatedBy      = "system",
                    LastUpdateDate = now,
                    LastUpdateBy   = "system"
                });
                await db.SaveChangesAsync();
            }
        }
        else
        {
            // Get sponsor's immediate children
            var children = await db.DualTeamTree
                .AsNoTracking()
                .Where(d => d.ParentMemberId == sponsorMemberId)
                .ToListAsync();

            var hasLeft  = children.Any(c => c.Side == TreeSide.Left);
            var hasRight = children.Any(c => c.Side == TreeSide.Right);

            if (!hasLeft)
            {
                // Rule 5.2.a — Sponsor has no children → place LEFT
                targetParentId = sponsorMemberId;
                targetSide     = TreeSide.Left;
            }
            else if (!hasRight)
            {
                // Rule 5.2.b — Sponsor has only left child → place RIGHT
                targetParentId = sponsorMemberId;
                targetSide     = TreeSide.Right;
            }
            else
            {
                // Rule 5.2.c — Sponsor has both children
                // Determine which side based on sponsor's position in their upline
                var preferredSide = DeterminePreferredSide(sponsorNode);

                // Find deepest available node on that side
                var (deepId, deepSide) = await FindDeepestAvailableNodeAsync(
                    db, sponsorMemberId, preferredSide);

                targetParentId = deepId;
                targetSide     = deepSide;
            }
        }

        // Get parent node for hierarchy path
        var parentNode = await db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == targetParentId);

        var parentPath = parentNode?.HierarchyPath ?? $"/{targetParentId}/";
        var newPath    = $"{parentPath}{memberId}/";

        // Create the node
        db.DualTeamTree.Add(new DualTeamEntity
        {
            MemberId       = memberId,
            ParentMemberId = targetParentId,
            Side           = targetSide,
            HierarchyPath  = newPath,
            CreationDate   = now,
            CreatedBy      = "system",
            LastUpdateDate = now,
            LastUpdateBy   = "system"
        });

        // Log the auto-placement
        db.PlacementLogs.Add(new PlacementLog
        {
            MemberId            = memberId,
            PlacedUnderMemberId = targetParentId,
            Side                = targetSide,
            Action              = "AutoPlaced",
            Reason              = "Auto-placement by system (window expired)",
            UnplacementCount    = 0,
            FirstPlacementDate  = now,
            CreationDate        = now,
            CreatedBy           = "system"
        });
    }

    /// <summary>
    /// Determines the preferred side for auto-placement based on the sponsor's
    /// own position within their upline's tree (rule 5.2.c).
    /// </summary>
    private static TreeSide DeterminePreferredSide(DualTeamEntity sponsorNode)
    {
        // If sponsor is on the left side of their parent → prefer LEFT
        // If sponsor is on the right side of their parent → prefer RIGHT
        // Default to LEFT if no parent
        return sponsorNode.ParentMemberId is null
            ? TreeSide.Left
            : sponsorNode.Side;
    }

    /// <summary>
    /// BFS walk on the given side of the sponsor's subtree to find
    /// the deepest available (leaf) node with an open slot.
    /// </summary>
    private async Task<(string NodeId, TreeSide Side)> FindDeepestAvailableNodeAsync(
        AppDbContext db, string sponsorMemberId, TreeSide preferredSide)
    {
        // Get the child on the preferred side
        var startChild = await db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ParentMemberId == sponsorMemberId
                                   && d.Side == preferredSide);

        if (startChild is null)
            return (sponsorMemberId, preferredSide);

        // BFS to find deepest available slot
        var queue   = new Queue<string>();
        var visited = new HashSet<string>();
        queue.Enqueue(startChild.MemberId);

        string bestNodeId   = startChild.MemberId;
        TreeSide bestSide   = TreeSide.Left;
        int bestDepth       = 0;

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!visited.Add(currentId)) continue;

            var children = await db.DualTeamTree
                .AsNoTracking()
                .Where(d => d.ParentMemberId == currentId)
                .ToListAsync();

            var hasLeft  = children.Any(c => c.Side == TreeSide.Left);
            var hasRight = children.Any(c => c.Side == TreeSide.Right);

            // This node has at least one open slot → candidate
            if (!hasLeft || !hasRight)
            {
                var currentNode = await db.DualTeamTree
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.MemberId == currentId);

                var depth = currentNode?.HierarchyPath.Count(c => c == '/') ?? 0;
                if (depth >= bestDepth)
                {
                    bestNodeId = currentId;
                    bestSide   = !hasLeft ? TreeSide.Left : TreeSide.Right;
                    bestDepth  = depth;
                }
            }

            foreach (var child in children)
                queue.Enqueue(child.MemberId);
        }

        return (bestNodeId, bestSide);
    }
}
