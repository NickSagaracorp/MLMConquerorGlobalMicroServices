using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Piernas del binario (LeftLegPoints/RightLegPoints, LeftLegCount/RightLegCount y la copia MemberStatistics.DualTeamPoints).
/// Plan Q (P-R9): quien escribe el binario ENCOLA un delta en DualLegDeltas (O(1), sin tocar la linea ascendente) y
/// DrainAsync los aplica por tandas, agregados por ascendiente. Los ascendientes salen de la clave con
/// dbo.DualAncestorKeys, sin recursion. Semantica (DQ2): un delta se aplica por los ascendientes que su arranque tenga
/// AL APLICARSE; los movimientos encolan lo que leen de las piernas ya aplicadas. En InMemory se aplica en el acto.
/// </summary>
public static class LegPointMaintenance
{
    /// <summary>Arranques distintos por tanda, como mucho.</summary>
    public const int BatchSize = 200;

    /// <summary>
    /// Revision I1: filas de ascendientes calculadas por tanda, como mucho (la suma de profundidad + 1 de sus arranques).
    /// Con la linea de 8.500 niveles caben unos 47 arranques por tanda en vez de 200 (1,7 millones de filas, que no
    /// cabian en el tiempo de un comando). Un arranque mas hondo que el tope va solo en su tanda.
    /// </summary>
    public const int MaxAncestorRowsPerBatch = 400_000;

    /// <summary>
    /// Revision I9: filas por UPDATE. Por debajo de 5.000, el umbral a partir del cual SQL Server escala los candados de
    /// fila de una sentencia a la tabla entera (y bloquearia colocaciones y el MERGE del trabajo de estadisticas).
    /// </summary>
    public const int UpdateChunk = 4_000;

    /// <summary>Revision I1: segundos de cada comando de una tanda, ademas de la espera del candado (el de EF es 30 s).</summary>
    public const int BatchCommandSeconds = 300;

    /// <summary>Revision C2: reintentos de una tanda si SQL Server la elige victima de un interbloqueo (1205).</summary>
    public const int MaxDeadlockRetries = 3;

    /// <summary>Encola la hoja recien guardada: sus PersonalPoints y 1 nodo, en la pierna de su padre.</summary>
    public static async Task EnqueueLeafAsync(AppDbContext db, string memberId, DateTime now, string by, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational()) { await ApplyInMemoryAsync(db, memberId, leaf: true, sign: 1, ct); return; }
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO DualLegDeltas (StartMemberId, Side, Points, NodeCount, CreationDate, CreatedBy)
SELECT d.ParentMemberId, d.Side,
       ISNULL((SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id), 0),
       1, {now}, {by}
FROM DualTeamTree d
WHERE d.MemberId = {memberId} AND d.IsDeleted = 0 AND d.ParentMemberId IS NOT NULL;", ct);
    }

    /// <summary>
    /// Encola el subarbol de <paramref name="subtreeRootId"/> entero (sus propios puntos mas sus dos piernas YA APLICADAS, y
    /// 1 + sus contadores) con el signo dado, en la pierna de su padre ACTUAL. -1 antes de soltarlo y +1 despues de colgarlo.
    /// </summary>
    public static async Task EnqueueSubtreeAsync(AppDbContext db, string subtreeRootId, int sign, DateTime now, string by,
        CancellationToken ct = default)
    {
        if (sign is not (1 or -1)) throw new ArgumentOutOfRangeException(nameof(sign));
        if (!db.Database.IsRelational()) { await ApplyInMemoryAsync(db, subtreeRootId, leaf: false, sign, ct); return; }
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO DualLegDeltas (StartMemberId, Side, Points, NodeCount, CreationDate, CreatedBy)
SELECT d.ParentMemberId, d.Side,
       {sign} * (ISNULL((SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id), 0)
                 + d.LeftLegPoints + d.RightLegPoints),
       {sign} * (1 + d.LeftLegCount + d.RightLegCount), {now}, {by}
FROM DualTeamTree d
WHERE d.MemberId = {subtreeRootId} AND d.IsDeleted = 0 AND d.ParentMemberId IS NOT NULL;", ct);
    }

    /// <summary>
    /// Aplica la cola hasta vaciar lo encolado al empezar (o hasta <paramref name="maxBatches"/> tandas) y devuelve cuantos
    /// deltas aplico. Cada tanda va en su transaccion; si quien llama ya tiene una (pruebas), trabaja dentro de ella sin
    /// confirmar y sin reintentar.
    /// </summary>
    public static async Task<int> DrainAsync(AppDbContext db, int maxBatches = int.MaxValue, CancellationToken ct = default,
        int lockTimeoutMs = DualTeamGate.DefaultLockTimeoutMs)
    {
        if (!db.Database.IsRelational()) return 0;

        // Revision I1: el tiempo de EF por comando es 30 s; una tanda espera el candado (hasta lockTimeoutMs) y despues
        // calcula. Con 30 s, un comando agotado se deshacia y la misma tanda se repetia sin fin (ORDER BY MIN(Id)).
        var antes = db.Database.GetCommandTimeout();
        db.Database.SetCommandTimeout(lockTimeoutMs / 1000 + BatchCommandSeconds);
        try
        {
            // Revision I2: solo lo encolado hasta ahora. Con altas entrando sin parar, «hasta que no quede nada» podia no
            // terminar nunca; lo que entre mientras tanto lo vacia la siguiente pasada (el trabajo corre cada minuto).
            var hasta = (await db.Database.SqlQueryRaw<long>("SELECT ISNULL(MAX(Id), 0) AS [Value] FROM DualLegDeltas")
                .ToListAsync(ct)).Single();
            if (hasta == 0) return 0;

            var total = 0;
            for (var i = 0; i < maxBatches; i++)
            {
                var n = await ApplyBatchAsync(db, hasta, lockTimeoutMs, ct);
                total += n;
                if (n == 0) break;
            }
            return total;
        }
        finally
        {
            db.Database.SetCommandTimeout(antes);
        }
    }

    /// <summary>Una tanda en su transaccion, reintentada si es victima de un interbloqueo (revision C2).</summary>
    private static async Task<int> ApplyBatchAsync(AppDbContext db, long hasta, int lockTimeoutMs, CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            var propia = db.Database.CurrentTransaction is null;
            await using var tx = propia ? await db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                await DualTeamGate.EnterLegDrainAsync(db, ct, lockTimeoutMs);
                var n = (await db.Database.SqlQueryRaw<int>(ApplyBatchSql,
                        new SqlParameter("@lote", BatchSize),
                        new SqlParameter("@maxFilas", MaxAncestorRowsPerBatch),
                        new SqlParameter("@trozo", UpdateChunk),
                        new SqlParameter("@hasta", hasta))
                    .ToListAsync(ct)).Single();
                if (tx is not null) await tx.CommitAsync(ct);
                return n;
            }
            catch (SqlException ex) when (ex.Number == 1205 && propia && intento < MaxDeadlockRetries)
            {
                // SQL Server ya deshizo la tanda; el await using cierra la transaccion y se repite entera.
            }
        }
    }

    // Una tanda:
    //  1. elige los arranques (hasta @lote, en orden de llegada, y hasta @maxFilas filas de ascendientes; el primero siempre
    //     entra) entre los deltas con Id <= @hasta;
    //  2. reclama (DELETE ... OUTPUT) todos sus deltas con Id <= @hasta;
    //  3. los agrega por arranque y lado, los expande a sus ascendientes por la clave y los agrega por ascendiente;
    //  4. aplica por trozos de @trozo filas (cada UPDATE por debajo del umbral de escalado de candados).
    // SET XACT_ABORT ON: cualquier error deshace la tanda entera (los deltas reclamados vuelven a la cola).
    private const string ApplyBatchSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
DROP TABLE IF EXISTS #arranques; DROP TABLE IF EXISTS #lote; DROP TABLE IF EXISTS #deltas; DROP TABLE IF EXISTS #x;

CREATE TABLE #arranques (StartMemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
WITH a AS (
    SELECT TOP (@lote) q.StartMemberId, MIN(q.Id) AS Primero
    FROM DualLegDeltas q
    WHERE q.Id <= @hasta
    GROUP BY q.StartMemberId
    ORDER BY MIN(q.Id)),
f AS (
    SELECT a.StartMemberId,
           SUM(ISNULL(d.Depth, 0) + 1) OVER (ORDER BY a.Primero ROWS UNBOUNDED PRECEDING) AS Acumulado,
           ROW_NUMBER() OVER (ORDER BY a.Primero) AS Rn
    FROM a
    LEFT JOIN DualTeamTree d ON d.MemberId = a.StartMemberId AND d.IsDeleted = 0)
INSERT INTO #arranques (StartMemberId)
SELECT StartMemberId FROM f WHERE Rn = 1 OR Acumulado <= @maxFilas;

CREATE TABLE #lote (StartMemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL, Side int NOT NULL,
                    Points decimal(18,4) NOT NULL, NodeCount int NOT NULL);
DELETE q
OUTPUT deleted.StartMemberId, deleted.Side, deleted.Points, deleted.NodeCount INTO #lote
FROM DualLegDeltas q
JOIN #arranques r ON r.StartMemberId = q.StartMemberId
WHERE q.Id <= @hasta;

SELECT a.AncestorKey,
       SUM(CASE WHEN a.Side = 0 THEN l.Points    ELSE 0 END) AS LP,
       SUM(CASE WHEN a.Side = 1 THEN l.Points    ELSE 0 END) AS RP,
       SUM(CASE WHEN a.Side = 0 THEN l.NodeCount ELSE 0 END) AS LC,
       SUM(CASE WHEN a.Side = 1 THEN l.NodeCount ELSE 0 END) AS RC
INTO #deltas
FROM (SELECT StartMemberId, Side, SUM(Points) AS Points, SUM(NodeCount) AS NodeCount FROM #lote GROUP BY StartMemberId, Side) l
JOIN DualTeamTree s ON s.MemberId = l.StartMemberId AND s.IsDeleted = 0 AND s.BinaryPath IS NOT NULL
CROSS APPLY dbo.DualAncestorKeys(s.BinaryPath, l.Side) a
GROUP BY a.AncestorKey
OPTION (MAXDOP 1);

SELECT d.Id AS NodeId, d.MemberId, x.LP, x.RP, x.LC, x.RC, ROW_NUMBER() OVER (ORDER BY d.Id) AS Rn
INTO #x
FROM #deltas x
JOIN DualTeamTree d ON d.BinaryPath = x.AncestorKey AND d.IsDeleted = 0
OPTION (MAXDOP 1);
CREATE CLUSTERED INDEX cx ON #x (Rn);

DECLARE @desde int = 1, @filas int = (SELECT COUNT(*) FROM #x);
WHILE @desde <= @filas
BEGIN
    UPDATE d
    SET LeftLegPoints  = d.LeftLegPoints  + x.LP,
        RightLegPoints = d.RightLegPoints + x.RP,
        LeftLegCount   = d.LeftLegCount   + x.LC,
        RightLegCount  = d.RightLegCount  + x.RC
    FROM DualTeamTree d
    JOIN #x x ON x.NodeId = d.Id
    WHERE x.Rn >= @desde AND x.Rn < @desde + @trozo;

    UPDATE s
    SET DualTeamPoints = CAST(d.LeftLegPoints + d.RightLegPoints AS int)
    FROM MemberStatistics s
    JOIN #x x ON x.MemberId = s.MemberId AND x.Rn >= @desde AND x.Rn < @desde + @trozo
    JOIN DualTeamTree d ON d.Id = x.NodeId;

    SET @desde += @trozo;
END

SELECT COUNT(*) AS [Value] FROM #lote;
DROP TABLE #x; DROP TABLE #deltas; DROP TABLE #lote; DROP TABLE #arranques;";

    // Plan Q: ApplyIncrementalAsync y ApplySubtreeAsync (subida en el acto) se retiran en la Tarea 7, cuando los siete
    // escritores pasen a encolar.
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
