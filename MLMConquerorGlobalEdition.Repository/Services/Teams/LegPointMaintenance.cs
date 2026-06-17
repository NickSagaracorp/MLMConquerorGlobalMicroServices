using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Incremental dual-team leg-point maintenance. When members are newly PLACED in the
/// binary tree (always as leaves, via the deepest-on-side spillover rule), each new
/// member's PersonalPoints must be added to every ancestor's leg — the leg being the
/// side of the ancestor's child through which the new member descends.
///
/// This replaces the old full-tree recompute (a <c>HierarchyPath LIKE</c> self-join that
/// was O(N²) and unusable at 1.5M nodes). Here we walk UP the adjacency list
/// (<c>ParentMemberId</c>) from each new leaf with a recursive CTE — cost is
/// O(new-members × depth), set-based, and depth-unbounded (<c>MAXRECURSION 0</c>), so it
/// scales with the deep chains the deepest-on-side rule produces.
/// </summary>
public static class LegPointMaintenance
{
    private const int ChunkSize = 500;

    /// <summary>
    /// Adds each newly-placed member's PersonalPoints up its ancestor chain (idempotent
    /// per placement — call exactly once when a member is first placed). Updates both
    /// DualTeamTree leg points and the cached MemberStatistics.DualTeamPoints for every
    /// affected ancestor.
    /// </summary>
    public static async Task ApplyIncrementalAsync(
        AppDbContext db, IReadOnlyCollection<string> placedMemberIds, CancellationToken ct = default)
    {
        if (placedMemberIds is null || placedMemberIds.Count == 0) return;

        foreach (var chunk in placedMemberIds.Distinct().Chunk(ChunkSize))
        {
            var values = string.Join(",", chunk.Select((_, i) => $"(@p{i})"));
            var sql = $@"
DECLARE @p TABLE (MemberId nvarchar(450) PRIMARY KEY);
INSERT INTO @p (MemberId) VALUES {values};

;WITH walk AS (
    -- seed: each newly placed leaf carries its own PersonalPoints + the side toward its parent
    SELECT d.MemberId, d.ParentMemberId, d.Side, s.PersonalPoints AS Pts
    FROM DualTeamTree d
    JOIN MemberStatistics s ON s.MemberId = d.MemberId
    WHERE d.MemberId IN (SELECT MemberId FROM @p)
    UNION ALL
    -- climb: the current node's parent becomes the node, still carrying the leaf's Pts,
    -- but now tagged with the parent's OWN side toward ITS parent
    SELECT pn.MemberId, pn.ParentMemberId, pn.Side, w.Pts
    FROM walk w
    JOIN DualTeamTree pn ON pn.MemberId = w.ParentMemberId
    WHERE w.ParentMemberId IS NOT NULL
)
SELECT ParentMemberId AS TargetId, Side, SUM(Pts) AS D
INTO #deltas
FROM walk
WHERE ParentMemberId IS NOT NULL
GROUP BY ParentMemberId, Side
OPTION (MAXRECURSION 0);

UPDATE d
SET LeftLegPoints  = d.LeftLegPoints  + COALESCE(l.D, 0),
    RightLegPoints = d.RightLegPoints + COALESCE(r.D, 0)
FROM DualTeamTree d
LEFT JOIN #deltas l ON l.TargetId = d.MemberId AND l.Side = 0
LEFT JOIN #deltas r ON r.TargetId = d.MemberId AND r.Side = 1
WHERE d.MemberId IN (SELECT TargetId FROM #deltas);

UPDATE s
SET DualTeamPoints = CAST(d.LeftLegPoints + d.RightLegPoints AS int)
FROM MemberStatistics s
JOIN DualTeamTree d ON d.MemberId = s.MemberId
WHERE d.MemberId IN (SELECT DISTINCT TargetId FROM #deltas);

DROP TABLE #deltas;";

            var ps = chunk.Select((id, i) => new SqlParameter($"@p{i}", id)).Cast<object>().ToArray();
            await db.Database.ExecuteSqlRawAsync(sql, ps, ct);
        }
    }
}
