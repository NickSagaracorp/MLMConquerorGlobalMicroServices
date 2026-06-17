using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

public record PlacementOutcome(string MemberId, string ParentMemberId, TreeSide Side, int Depth, bool AlreadyPlaced);
public record BulkPlacementOutcome(int Placed, int Skipped, int Failed);

/// <summary>
/// The single authority for inserting members into the dual-team binary tree. Replaces the
/// per-write-site bespoke logic (which raced and produced duplicate / collided rows).
///
/// Placement rules (deepest-chain spillover):
///   a) sponsor has no left child   → place LEFT under sponsor
///   b) sponsor has left, no right  → place RIGHT under sponsor
///   c) sponsor has both children   → spill to the DEEPEST open node on the sponsor's
///      preferred side (the sponsor's own Side; Left for a root) and place there.
///
/// Scalability: the deepest node is cached per sponsor in <see cref="DualTeamLegFrontier"/>
/// so step (c) is O(1) instead of an O(subtree) BFS. The cache self-heals if stale.
/// Concurrency: <see cref="PlaceAsync"/> serializes per sponsor via sp_getapplock so two
/// placements never grab the same slot; the unique IX_DualTeamTree_MemberId index is the
/// hard backstop. Leg points are maintained incrementally (walk-up), never a full recompute.
/// </summary>
public interface IPlacementService
{
    /// <summary>Place one member. Transactional, idempotent, concurrency-safe. Maintains leg points.</summary>
    Task<Result<PlacementOutcome>> PlaceAsync(string memberId, string sponsorMemberId, CancellationToken ct = default);

    /// <summary>
    /// Place many members efficiently (rank-climb / backfill path). Leg-point maintenance is
    /// deferred and applied once at the end. Intended for single-threaded bulk runs.
    /// </summary>
    Task<Result<BulkPlacementOutcome>> PlaceBulkAsync(
        IReadOnlyList<(string MemberId, string SponsorMemberId)> pairs, CancellationToken ct = default);
}

public class PlacementService : IPlacementService
{
    private const int BulkSaveChunk = 1000;

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public PlacementService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    private static int Depth(string path) => DualTeamPlacementRules.Depth(path);
    private static string ChildPath(string parentPath, string memberId)
        => DualTeamPlacementRules.ChildPath(parentPath, memberId);

    public async Task<Result<PlacementOutcome>> PlaceAsync(
        string memberId, string sponsorMemberId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(memberId))
            return Result<PlacementOutcome>.Failure("INVALID_MEMBER", "memberId is required.");
        if (string.IsNullOrWhiteSpace(sponsorMemberId))
            return Result<PlacementOutcome>.Failure("INVALID_SPONSOR", "sponsorMemberId is required.");

        // Fast idempotency check outside the transaction.
        var existing = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
        if (existing is not null)
            return Result<PlacementOutcome>.Success(
                new PlacementOutcome(memberId, existing.ParentMemberId ?? "", existing.Side, Depth(existing.HierarchyPath), true));

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Serialize placements into this sponsor's subtree. Different sponsors run in
            // parallel; same-sponsor placements queue here, preventing duplicate slot grabs.
            await _db.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;",
                new object[] { sponsorMemberId }, ct);

            // Re-check inside the lock (another writer may have placed us).
            if (await _db.DualTeamTree.AnyAsync(d => d.MemberId == memberId, ct))
            {
                var now2 = await _db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == memberId, ct);
                await tx.CommitAsync(ct);
                return Result<PlacementOutcome>.Success(
                    new PlacementOutcome(memberId, now2.ParentMemberId ?? "", now2.Side, Depth(now2.HierarchyPath), true));
            }

            var target = await ResolveTargetAsync(sponsorMemberId, ct);

            var node = NewNode(memberId, target.ParentId, target.Side, target.ParentPath);
            _db.DualTeamTree.Add(node);
            _db.PlacementLogs.Add(NewLog(memberId, target.ParentId, target.Side));

            await UpsertFrontierAsync(sponsorMemberId, target, memberId, ct);

            await _db.SaveChangesAsync(ct);

            // Incremental leg-point roll-up for this one new leaf (participates in this tx).
            await LegPointMaintenance.ApplyIncrementalAsync(_db, new[] { memberId }, ct);

            await tx.CommitAsync(ct);
            return Result<PlacementOutcome>.Success(
                new PlacementOutcome(memberId, target.ParentId, target.Side, Depth(node.HierarchyPath), false));
        }
        catch (DbUpdateException)
        {
            // Possibly lost a race to the unique MemberId index — if the member is now
            // present, treat as success (idempotent); otherwise surface the failure.
            await tx.RollbackAsync(ct);
            var n = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
            if (n is not null)
                return Result<PlacementOutcome>.Success(
                    new PlacementOutcome(memberId, n.ParentMemberId ?? "", n.Side, Depth(n.HierarchyPath), true));
            return Result<PlacementOutcome>.Failure("PLACEMENT_CONFLICT", "Concurrent placement conflict.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return Result<PlacementOutcome>.Failure("PLACEMENT_FAILED", ex.Message);
        }
    }

    private sealed record Target(string ParentId, TreeSide Side, string ParentPath, bool IsSpill, TreeSide PreferredSide);

    /// <summary>Resolve the placement slot for a sponsor by rules a/b/c (queries the live tree).</summary>
    private async Task<Target> ResolveTargetAsync(string sponsorMemberId, CancellationToken ct)
    {
        var sponsorNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == sponsorMemberId, ct);

        if (sponsorNode is null)
        {
            // Sponsor isn't in the tree yet → create it as a root, then place under it (Left).
            var root = NewNode(sponsorMemberId, null, TreeSide.Left, null);
            _db.DualTeamTree.Add(root);
            await _db.SaveChangesAsync(ct);
            return new Target(sponsorMemberId, TreeSide.Left, root.HierarchyPath, false, TreeSide.Left);
        }

        var preferred = sponsorNode.ParentMemberId is null ? TreeSide.Left : sponsorNode.Side;

        var children = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == sponsorMemberId)
            .Select(d => d.Side)
            .ToListAsync(ct);
        var hasLeft = children.Contains(TreeSide.Left);
        var hasRight = children.Contains(TreeSide.Right);

        string? deepId = null;
        string deepPath = sponsorNode.HierarchyPath;
        bool deepHasLeft = false;
        if (hasLeft && hasRight)
        {
            // Rule c — spill to the deepest open node on the preferred side.
            (deepId, deepPath) = await ResolveDeepestAsync(sponsorMemberId, preferred, ct);
            deepHasLeft = await _db.DualTeamTree.AsNoTracking()
                .AnyAsync(d => d.ParentMemberId == deepId && d.Side == TreeSide.Left, ct);
        }

        var decision = DualTeamPlacementRules.Decide(sponsorMemberId, hasLeft, hasRight, deepId, deepHasLeft);
        var parentPath = decision.IsSpill ? deepPath : sponsorNode.HierarchyPath;
        return new Target(decision.ParentMemberId, decision.Side, parentPath, decision.IsSpill, preferred);
    }

    /// <summary>
    /// Deepest open node on the sponsor's preferred side. Reads the cached frontier first
    /// (O(1)); falls back to a BFS descent if there's no cache or the cache is stale.
    /// </summary>
    private async Task<(string Id, string Path)> ResolveDeepestAsync(
        string sponsorMemberId, TreeSide preferred, CancellationToken ct)
    {
        var frontier = await _db.DualTeamLegFrontiers.AsNoTracking()
            .FirstOrDefaultAsync(f => f.SponsorMemberId == sponsorMemberId, ct);

        if (frontier?.DeepestMemberId is not null)
        {
            var cached = await _db.DualTeamTree.AsNoTracking()
                .FirstOrDefaultAsync(d => d.MemberId == frontier.DeepestMemberId, ct);
            if (cached is not null)
            {
                var hasBoth = await _db.DualTeamTree.AsNoTracking()
                    .CountAsync(d => d.ParentMemberId == cached.MemberId, ct) >= 2;
                if (!hasBoth) return (cached.MemberId, cached.HierarchyPath); // cache fresh
            }
            // else: stale → fall through to BFS self-heal
        }

        // BFS the preferred-side subtree for the deepest node with an open slot.
        var legRoot = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ParentMemberId == sponsorMemberId && d.Side == preferred, ct);
        if (legRoot is null)
        {
            var sponsor = await _db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == sponsorMemberId, ct);
            return (sponsor.MemberId, sponsor.HierarchyPath);
        }

        var queue = new Queue<(string Id, string Path)>();
        queue.Enqueue((legRoot.MemberId, legRoot.HierarchyPath));
        var best = (legRoot.MemberId, legRoot.HierarchyPath);
        var bestDepth = Depth(legRoot.HierarchyPath);

        while (queue.Count > 0)
        {
            var (curId, curPath) = queue.Dequeue();
            var kids = await _db.DualTeamTree.AsNoTracking()
                .Where(d => d.ParentMemberId == curId)
                .Select(d => new { d.MemberId, d.HierarchyPath, d.Side })
                .ToListAsync(ct);

            if (kids.Count < 2)
            {
                var depth = Depth(curPath);
                if (depth >= bestDepth) { best = (curId, curPath); bestDepth = depth; }
            }
            foreach (var k in kids) queue.Enqueue((k.MemberId, k.HierarchyPath));
        }
        return best;
    }

    private async Task UpsertFrontierAsync(string sponsorMemberId, Target target, string newMemberId, CancellationToken ct)
    {
        // The frontier tracks the deepest node on the preferred side. It advances when this
        // placement extends the preferred-side chain: a spill, or the sponsor's preferred-side
        // direct child (rule a/b landing on the preferred side).
        var decision = new DualTeamPlacementRules.SlotDecision(target.ParentId, target.Side, target.IsSpill);
        if (!DualTeamPlacementRules.ExtendsPreferredSide(decision, sponsorMemberId, target.PreferredSide)) return;

        var newDepth = Depth(ChildPath(target.ParentPath, newMemberId));
        var frontier = await _db.DualTeamLegFrontiers
            .FirstOrDefaultAsync(f => f.SponsorMemberId == sponsorMemberId, ct);
        if (frontier is null)
        {
            _db.DualTeamLegFrontiers.Add(new DualTeamLegFrontier
            {
                SponsorMemberId = sponsorMemberId,
                PreferredSide = target.PreferredSide,
                DeepestMemberId = newMemberId,
                DeepestDepth = newDepth
            });
        }
        else if (newDepth >= frontier.DeepestDepth)
        {
            frontier.PreferredSide = target.PreferredSide;
            frontier.DeepestMemberId = newMemberId;
            frontier.DeepestDepth = newDepth;
        }
    }

    private DualTeamEntity NewNode(string memberId, string? parentId, TreeSide side, string? parentPath)
    {
        var now = _clock.Now;
        return new DualTeamEntity
        {
            MemberId = memberId,
            ParentMemberId = parentId,
            Side = side,
            HierarchyPath = parentPath is null ? $"/{memberId}/" : ChildPath(parentPath, memberId),
            LeftLegPoints = 0,
            RightLegPoints = 0,
            CreationDate = now,
            CreatedBy = "placement-service",
            LastUpdateDate = now
        };
    }

    private PlacementLog NewLog(string memberId, string parentId, TreeSide side)
    {
        var now = _clock.Now;
        return new PlacementLog
        {
            MemberId = memberId,
            PlacedUnderMemberId = parentId,
            Side = side,
            Action = "Placed",
            Reason = "Placement engine",
            UnplacementCount = 0,
            FirstPlacementDate = now,
            CreationDate = now,
            CreatedBy = "placement-service"
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Bulk path
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class LegState
    {
        public bool SponsorInTree;
        public string SponsorPath = "";
        public bool HasLeft;
        public bool HasRight;
        public TreeSide PreferredSide;
        public string? DeepestId;
        public string DeepestPath = "";
        public int DeepestDepth;
    }

    public async Task<Result<BulkPlacementOutcome>> PlaceBulkAsync(
        IReadOnlyList<(string MemberId, string SponsorMemberId)> pairs, CancellationToken ct = default)
    {
        if (pairs is null || pairs.Count == 0)
            return Result<BulkPlacementOutcome>.Success(new BulkPlacementOutcome(0, 0, 0));

        // Skip members already placed (idempotent) and intra-batch duplicates.
        var ids = pairs.Select(p => p.MemberId).Distinct().ToList();
        var alreadyPlaced = new HashSet<string>(
            await _db.DualTeamTree.AsNoTracking()
                .Where(d => ids.Contains(d.MemberId)).Select(d => d.MemberId).ToListAsync(ct));

        var states = new Dictionary<string, LegState>();
        var placedThisRun = new HashSet<string>(alreadyPlaced);
        var placedIds = new List<string>();
        var states_touched = new HashSet<string>();
        int placed = 0, skipped = 0, failed = 0;
        int sinceSave = 0;

        try
        {
            foreach (var (memberId, sponsorId) in pairs)
            {
                if (string.IsNullOrWhiteSpace(memberId) || string.IsNullOrWhiteSpace(sponsorId)) { failed++; continue; }
                if (placedThisRun.Contains(memberId)) { skipped++; continue; }

                var st = await GetOrSeedStateAsync(states, sponsorId, ct);
                states_touched.Add(sponsorId);

                string parentId; TreeSide side; string parentPath; bool isSpill;

                if (!st.SponsorInTree)
                {
                    // Create sponsor root, then place this member as its left child.
                    var root = NewNode(sponsorId, null, TreeSide.Left, null);
                    _db.DualTeamTree.Add(root);
                    placedThisRun.Add(sponsorId);
                    st.SponsorInTree = true;
                    st.SponsorPath = root.HierarchyPath;
                    st.PreferredSide = TreeSide.Left;
                    parentId = sponsorId; side = TreeSide.Left; parentPath = root.HierarchyPath;
                    isSpill = false;
                }
                else
                {
                    // In-batch the deepest node is always a freshly stacked chain tail (no left child).
                    var decision = DualTeamPlacementRules.Decide(
                        sponsorId, st.HasLeft, st.HasRight, st.DeepestId, deepestNodeHasLeftChild: false);
                    parentId = decision.ParentMemberId;
                    side = decision.Side;
                    isSpill = decision.IsSpill;
                    parentPath = isSpill && st.DeepestId is not null ? st.DeepestPath : st.SponsorPath;
                }

                var node = NewNode(memberId, parentId, side, parentPath);
                _db.DualTeamTree.Add(node);
                _db.PlacementLogs.Add(NewLog(memberId, parentId, side));
                placedThisRun.Add(memberId);
                placedIds.Add(memberId);
                placed++;

                // Advance in-memory state.
                if (parentId == sponsorId && side == TreeSide.Left) st.HasLeft = true;
                if (parentId == sponsorId && side == TreeSide.Right) st.HasRight = true;

                var decisionForFrontier = new DualTeamPlacementRules.SlotDecision(parentId, side, isSpill);
                var extendsPreferred = DualTeamPlacementRules.ExtendsPreferredSide(decisionForFrontier, sponsorId, st.PreferredSide);
                if (extendsPreferred && Depth(node.HierarchyPath) >= st.DeepestDepth)
                {
                    st.DeepestId = memberId;
                    st.DeepestPath = node.HierarchyPath;
                    st.DeepestDepth = Depth(node.HierarchyPath);
                }

                if (++sinceSave >= BulkSaveChunk) { await _db.SaveChangesAsync(ct); sinceSave = 0; }
            }

            if (sinceSave > 0) await _db.SaveChangesAsync(ct);

            // One deferred leg-point pass over everything newly placed.
            if (placedIds.Count > 0)
                await LegPointMaintenance.ApplyIncrementalAsync(_db, placedIds, ct);

            // Persist frontier cache for every sponsor we touched.
            await FlushFrontiersAsync(states, ct);

            return Result<BulkPlacementOutcome>.Success(new BulkPlacementOutcome(placed, skipped, failed));
        }
        catch (Exception ex)
        {
            return Result<BulkPlacementOutcome>.Failure("BULK_PLACEMENT_FAILED", ex.Message);
        }
    }

    private async Task<LegState> GetOrSeedStateAsync(
        Dictionary<string, LegState> states, string sponsorId, CancellationToken ct)
    {
        if (states.TryGetValue(sponsorId, out var s)) return s;

        s = new LegState();
        var sponsorNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == sponsorId, ct);
        if (sponsorNode is not null)
        {
            s.SponsorInTree = true;
            s.SponsorPath = sponsorNode.HierarchyPath;
            s.PreferredSide = sponsorNode.ParentMemberId is null ? TreeSide.Left : sponsorNode.Side;

            var childSides = await _db.DualTeamTree.AsNoTracking()
                .Where(d => d.ParentMemberId == sponsorId).Select(d => d.Side).ToListAsync(ct);
            s.HasLeft = childSides.Contains(TreeSide.Left);
            s.HasRight = childSides.Contains(TreeSide.Right);

            if (s.HasLeft && s.HasRight)
            {
                var (deepId, deepPath) = await ResolveDeepestAsync(sponsorId, s.PreferredSide, ct);
                s.DeepestId = deepId;
                s.DeepestPath = deepPath;
                s.DeepestDepth = Depth(deepPath);
            }
        }
        states[sponsorId] = s;
        return s;
    }

    private async Task FlushFrontiersAsync(Dictionary<string, LegState> states, CancellationToken ct)
    {
        var sponsorIds = states.Keys.ToList();
        var existing = await _db.DualTeamLegFrontiers
            .Where(f => sponsorIds.Contains(f.SponsorMemberId)).ToListAsync(ct);
        var byId = existing.ToDictionary(f => f.SponsorMemberId);

        foreach (var (sponsorId, st) in states)
        {
            if (st.DeepestId is null) continue;
            if (byId.TryGetValue(sponsorId, out var f))
            {
                f.PreferredSide = st.PreferredSide;
                f.DeepestMemberId = st.DeepestId;
                f.DeepestDepth = st.DeepestDepth;
            }
            else
            {
                _db.DualTeamLegFrontiers.Add(new DualTeamLegFrontier
                {
                    SponsorMemberId = sponsorId,
                    PreferredSide = st.PreferredSide,
                    DeepestMemberId = st.DeepestId,
                    DeepestDepth = st.DeepestDepth
                });
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
