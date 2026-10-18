using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Incremental dual-team leg maintenance: points (LeftLegPoints/RightLegPoints and the cached
/// MemberStatistics.DualTeamPoints) and, since Plan P, node counts (LeftLegCount/RightLegCount).
///
/// A node's leg = everything in the subtree of its child on that side (the child included). When a
/// subtree appears under a node (a new leaf, a moved subtree, a provisional root being placed) its
/// totals are ADDED to every ancestor through the side it descends by; when it leaves (a removal or the
/// old position of a move) they are SUBTRACTED. Walking UP the adjacency list (ParentMemberId) with a
/// recursive CTE costs O(depth) per subtree, never a recount of the tree (Plan P, C2 of the review: the
/// path-based recalculator was O(depth × N) inside the tree lock).
/// </summary>
public static class LegPointMaintenance
{
    private const int ChunkSize = 500;

    /// <summary>
    /// Adds each newly-placed LEAF's PersonalPoints and a count of 1 up its ancestor chain. Call exactly once
    /// per new leaf, after it has been saved.
    /// </summary>
    public static async Task ApplyIncrementalAsync(
        AppDbContext db, IReadOnlyCollection<string> placedMemberIds, CancellationToken ct = default)
    {
        if (placedMemberIds is null || placedMemberIds.Count == 0) return;
        if (!db.Database.IsRelational())
        {
            foreach (var id in placedMemberIds.Distinct())
                await ApplyInMemoryAsync(db, id, leaf: true, sign: 1, ct);
            return;
        }

        foreach (var chunk in placedMemberIds.Distinct().Chunk(ChunkSize))
        {
            var values = string.Join(",", chunk.Select((_, i) => $"(@p{i})"));
            var seed = $@"
INSERT INTO @s (MemberId, Pts, C)
SELECT v.MemberId,
       ISNULL((SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = v.MemberId ORDER BY ms.Id), 0),
       1
FROM (VALUES {values}) v(MemberId);";
            var ps = chunk.Select((id, i) => new SqlParameter($"@p{i}", id)).Cast<object>().ToArray();
            await db.Database.ExecuteSqlRawAsync(Walk(seed), ps, ct);
        }
    }

    /// <summary>
    /// Adds (<paramref name="sign"/> = 1) or subtracts (-1) the whole subtree of <paramref name="subtreeRootId"/>
    /// — its own PersonalPoints plus its two legs, and 1 + its two leg counts — on every ancestor of its CURRENT
    /// position. Call with -1 before detaching it from its parent and with +1 after attaching it.
    /// </summary>
    public static async Task ApplySubtreeAsync(
        AppDbContext db, string subtreeRootId, int sign, CancellationToken ct = default)
    {
        if (sign is not (1 or -1)) throw new ArgumentOutOfRangeException(nameof(sign));
        if (!db.Database.IsRelational())
        {
            await ApplyInMemoryAsync(db, subtreeRootId, leaf: false, sign, ct);
            return;
        }

        const string seed = @"
INSERT INTO @s (MemberId, Pts, C)
SELECT d.MemberId,
       @sign * (ISNULL((SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id), 0)
                + d.LeftLegPoints + d.RightLegPoints),
       @sign * (1 + d.LeftLegCount + d.RightLegCount)
FROM DualTeamTree d
WHERE d.MemberId = @root AND d.IsDeleted = 0;";
        await db.Database.ExecuteSqlRawAsync(Walk(seed),
            new object[] { new SqlParameter("@root", subtreeRootId), new SqlParameter("@sign", sign) }, ct);
    }

    private static string Walk(string seed) => $@"
DECLARE @s TABLE (MemberId nvarchar(450) PRIMARY KEY, Pts decimal(18,4) NOT NULL, C int NOT NULL);
{seed}

;WITH walk AS (
    -- seed: the subtree root carries its totals + the side toward its parent
    SELECT d.MemberId, d.ParentMemberId, d.Side, s.Pts, s.C
    FROM DualTeamTree d
    JOIN @s s ON s.MemberId = d.MemberId
    WHERE d.IsDeleted = 0
    UNION ALL
    -- climb: the parent becomes the node, still carrying the totals, tagged with ITS side toward ITS parent
    SELECT pn.MemberId, pn.ParentMemberId, pn.Side, w.Pts, w.C
    FROM walk w
    JOIN DualTeamTree pn ON pn.MemberId = w.ParentMemberId AND pn.IsDeleted = 0
    WHERE w.ParentMemberId IS NOT NULL
)
SELECT ParentMemberId AS TargetId, Side, SUM(Pts) AS D, SUM(C) AS C
INTO #deltas
FROM walk
WHERE ParentMemberId IS NOT NULL
GROUP BY ParentMemberId, Side
OPTION (MAXRECURSION 0, MAXDOP 1);

UPDATE d
SET LeftLegPoints  = d.LeftLegPoints  + COALESCE(l.D, 0),
    RightLegPoints = d.RightLegPoints + COALESCE(r.D, 0),
    LeftLegCount   = d.LeftLegCount   + COALESCE(l.C, 0),
    RightLegCount  = d.RightLegCount  + COALESCE(r.C, 0)
FROM DualTeamTree d
LEFT JOIN #deltas l ON l.TargetId = d.MemberId AND l.Side = 0
LEFT JOIN #deltas r ON r.TargetId = d.MemberId AND r.Side = 1
WHERE d.IsDeleted = 0 AND d.MemberId IN (SELECT TargetId FROM #deltas)
OPTION (MAXDOP 1);

UPDATE s
SET DualTeamPoints = CAST(d.LeftLegPoints + d.RightLegPoints AS int)
FROM MemberStatistics s
JOIN DualTeamTree d ON d.MemberId = s.MemberId AND d.IsDeleted = 0
WHERE d.MemberId IN (SELECT TargetId FROM #deltas)
OPTION (MAXDOP 1);

DROP TABLE #deltas;";

    /// <summary>La misma semantica sobre el proveedor InMemory (pruebas de logica): sube nodo a nodo.</summary>
    private static async Task ApplyInMemoryAsync(AppDbContext db, string rootId, bool leaf, int sign, CancellationToken ct)
    {
        var node = await db.DualTeamTree.FirstOrDefaultAsync(d => d.MemberId == rootId, ct);
        if (node is null) return;
        var personal = await db.MemberStatistics.Where(s => s.MemberId == rootId).OrderBy(s => s.Id)
            .Select(s => (decimal?)s.PersonalPoints).FirstOrDefaultAsync(ct) ?? 0m;
        var pts = sign * (leaf ? personal : personal + node.LeftLegPoints + node.RightLegPoints);
        var cnt = sign * (leaf ? 1 : 1 + node.LeftLegCount + node.RightLegCount);

        var actual = node;
        var visitados = new HashSet<string> { actual.MemberId };
        while (actual.ParentMemberId is not null)
        {
            var padre = await db.DualTeamTree.FirstOrDefaultAsync(d => d.MemberId == actual.ParentMemberId, ct);
            if (padre is null || !visitados.Add(padre.MemberId)) break;
            if (actual.Side == TreeSide.Left) { padre.LeftLegPoints += pts; padre.LeftLegCount += cnt; }
            else { padre.RightLegPoints += pts; padre.RightLegCount += cnt; }
            var stats = await db.MemberStatistics.Where(s => s.MemberId == padre.MemberId).OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
            if (stats is not null) stats.DualTeamPoints = (int)(padre.LeftLegPoints + padre.RightLegPoints);
            actual = padre;
        }
        await db.SaveChangesAsync(ct);
    }
}
