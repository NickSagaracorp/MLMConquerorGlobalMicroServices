using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Plan Q (revision del bloque 2): tamanos de tanda del vaciado. Las pruebas los bajan para forzar varias tandas.</summary>
public sealed record LegDrainOptions
{
    /// <summary>Filas de la cola que mira cada tanda (en orden de Id); sus arranques distintos son la tanda.</summary>
    public int WindowRows { get; init; } = LegPointMaintenance.WindowRows;
    /// <summary>Ascendientes distintos que expande una tanda, como mucho (la union de los caminos de sus arranques).</summary>
    public int MaxPathRows { get; init; } = LegPointMaintenance.MaxPathRowsPerBatch;
    /// <summary>Filas por UPDATE.</summary>
    public int UpdateChunk { get; init; } = LegPointMaintenance.UpdateChunk;
    /// <summary>Segundos de cada comando de una tanda, ademas de la espera del candado.</summary>
    public int BatchCommandSeconds { get; init; } = LegPointMaintenance.BatchCommandSeconds;
}

/// <summary>
/// Piernas del binario (LeftLegPoints/RightLegPoints, LeftLegCount/RightLegCount, los calificados y las copias
/// MemberStatistics.DualTeamPoints/DualTeamSize).
/// Plan Q (P-R9): quien escribe el binario ENCOLA un delta en DualLegDeltas (O(1), sin tocar la linea ascendente) y
/// DrainAsync los aplica por tandas. Semantica (DQ2): un delta se aplica por los ascendientes que su arranque tenga AL
/// APLICARSE; los movimientos encolan lo que leen de las piernas ya aplicadas. En InMemory se aplica en el acto.
/// Revision del bloque 2 (I-3): una tanda expande la UNION de los caminos de sus arranques (cada ascendiente distinto una
/// sola vez) en lugar de un camino entero por arranque, y suma por ascendiente y lado con rangos sobre los arranques
/// ordenados por la clave (inorden: los que comparten un prefijo son contiguos).
/// </summary>
public static class LegPointMaintenance
{
    /// <summary>Filas de la cola que mira cada tanda, como mucho.</summary>
    public const int WindowRows = 1_000;

    /// <summary>
    /// Ascendientes distintos (la union de los caminos) que expande una tanda, como mucho. Con la linea de 8.200 niveles,
    /// 1.000 arranques en la misma pierna son unos 9.000 y se vacian en una tanda de unos 6 s (medido en local). El tope
    /// solo actua con arranques en muchas ramas hondas distintas (unas 25 ramas de 8.000 niveles): con la expansion de la
    /// Tarea 10 (82.000 filas en 2 a 3,4 s), 200.000 filas son unos 5 a 8 s por tanda, lejos del tiempo del comando. Si la
    /// union de la ventana pasa del tope, la tanda se queda con la primera mitad de sus arranques (por orden de llegada)
    /// hasta que cabe o queda uno.
    /// </summary>
    public const int MaxPathRowsPerBatch = 200_000;

    /// <summary>
    /// Revision I9: filas por UPDATE. Por debajo de 5.000, el umbral a partir del cual SQL Server escala los candados de
    /// fila de una sentencia a la tabla entera (y bloquearia colocaciones y el MERGE del trabajo de estadisticas).
    /// </summary>
    public const int UpdateChunk = 4_000;

    /// <summary>Revision I1: segundos de cada comando de una tanda, ademas de la espera del candado (el de EF es 30 s).</summary>
    public const int BatchCommandSeconds = 300;

    /// <summary>Revision C2: reintentos de una tanda si SQL Server la elige victima de un interbloqueo (1205).</summary>
    public const int MaxDeadlockRetries = 3;

    /// <summary>
    /// Revision del bloque 2 (M-3): fallos de una tanda de un solo arranque antes de apartar sus deltas a
    /// DualLegDeltaDeadLetters. Cada fallo termina la pasada, asi que son tres pasadas (unos tres minutos).
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Revision 2 del bloque 2 (I1): los UNICOS errores que pueden ser culpa de los datos de un arranque (desbordamiento,
    /// conversion, division por cero, clave repetida, FK, nulo, truncado). Solo estos parten la ventana y anotan intentos, y
    /// solo en la tanda de un solo arranque cuando el mismo error se repite al rehacerla (tercera revision del bloque 2: la
    /// clave repetida y la FK pueden ser carreras con otro escritor, y una carrera no se repite).
    /// Cualquier otro (tiempo agotado -2; conexion o conmutacion -1, 53, 233, 10054, 40613, 4060; registro lleno 9002;
    /// archivo lleno 1105; memoria 701 y 8645; espera de candado 1222; un fallo del propio SQL de la tanda) no depende de
    /// ningun arranque: se propaga sin anotar nada, para no apartar deltas sanos (dinero) por un problema del servidor.
    /// </summary>
    public static readonly IReadOnlySet<int> DataErrors =
        new HashSet<int> { 8115, 8114, 220, 232, 517, 8134, 245, 2627, 2601, 547, 515, 8152, 2628 };

    /// <summary>
    /// Revision 2 del bloque 2 (I1): los deltas apartados en DualLegDeltaDeadLetters (Q-R30). Debe ser 0: el trabajo de cada
    /// minuto lo registra como error y la salud del sistema (AdminAPI) queda degradada mientras no lo sea.
    /// </summary>
    public static Task<int> DeadLetterCountAsync(AppDbContext db, CancellationToken ct = default) =>
        db.DualLegDeltaDeadLetters.AsNoTracking().CountAsync(ct);

    /// <summary>
    /// Encola la hoja recien guardada: sus puntos propios APLICADOS y 1 nodo, en la pierna de su padre. Calificados 0 (revision del
    /// bloque 2, M-7): un nodo nuevo nace con SelfQualified = 0, y sus activas le llegan como propios aparcados al vaciar.
    /// </summary>
    public static async Task EnqueueLeafAsync(AppDbContext db, string memberId, DateTime now, string by, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational()) { await ApplyInMemoryAsync(db, memberId, leaf: true, sign: 1, ct); return; }
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO DualLegDeltas (StartMemberId, Side, Points, NodeCount, QualifiedCount, OwnChange, CreationDate, CreatedBy)
SELECT d.ParentMemberId, d.Side,
       ISNULL((SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id), 0)
         - ISNULL((SELECT SUM(p.Points) FROM DualLegDeltas p WHERE p.StartMemberId = d.MemberId AND p.OwnChange = 1), 0),
       1, 0, 0, {now}, {by}
FROM DualTeamTree d
WHERE d.MemberId = {memberId} AND d.IsDeleted = 0 AND d.ParentMemberId IS NOT NULL;", ct);
    }

    /// <summary>
    /// Plan Q (DQ16): los puntos propios aplicados de un miembro son sus PersonalPoints MENOS sus propios de puntos pendientes
    /// en la cola (los encola MembershipPointsDeltas y llegan al vaciar por la linea que tenga entonces, DQ2). La hoja y el
    /// subarbol llevan solo lo aplicado: asi lo pendiente no cuenta dos veces ni se pierde en un movimiento o una retirada.
    /// Encola el subarbol de <paramref name="subtreeRootId"/> entero (sus propios puntos mas sus dos piernas YA APLICADAS, y
    /// 1 + sus contadores) con el signo dado, en la pierna de su padre ACTUAL. -1 antes de soltarlo y +1 despues de colgarlo.
    /// </summary>
    public static async Task EnqueueSubtreeAsync(AppDbContext db, string subtreeRootId, int sign, DateTime now, string by,
        CancellationToken ct = default)
    {
        if (sign is not (1 or -1)) throw new ArgumentOutOfRangeException(nameof(sign));
        if (!db.Database.IsRelational()) { await ApplyInMemoryAsync(db, subtreeRootId, leaf: false, sign, ct); return; }
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO DualLegDeltas (StartMemberId, Side, Points, NodeCount, QualifiedCount, OwnChange, CreationDate, CreatedBy)
SELECT d.ParentMemberId, d.Side,
       {sign} * (ISNULL((SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id), 0)
                 - ISNULL((SELECT SUM(p.Points) FROM DualLegDeltas p WHERE p.StartMemberId = d.MemberId AND p.OwnChange = 1), 0)
                 + d.LeftLegPoints + d.RightLegPoints),
       {sign} * (1 + d.LeftLegCount + d.RightLegCount),
       {sign} * (d.SelfQualified + d.LeftLegQualified + d.RightLegQualified), 0, {now}, {by}
FROM DualTeamTree d
WHERE d.MemberId = {subtreeRootId} AND d.IsDeleted = 0 AND d.ParentMemberId IS NOT NULL;", ct);
    }

    /// <summary>
    /// Aplica la cola hasta vaciar lo encolado al empezar (o hasta <paramref name="maxBatches"/> tandas) y devuelve cuantos
    /// deltas aplico. Cada tanda va en su transaccion; si quien llama ya tiene una (pruebas), trabaja dentro de ella sin
    /// confirmar, sin reintentar y sin apartar deltas por fallo.
    /// <paramref name="isolateFailures"/> (revision 2 del bloque 2, I1): solo el trabajo de cada minuto
    /// (ApplyDualLegDeltasJob) aisla un fallo de datos partiendo la ventana y anotando intentos. Los demas (comisiones,
    /// instantanea, rango) propagan el error en el acto: no pueden quedarse decenas de minutos aislando un arranque.
    /// </summary>
    public static async Task<int> DrainAsync(AppDbContext db, int maxBatches = int.MaxValue, CancellationToken ct = default,
        int lockTimeoutMs = DualTeamGate.DefaultLockTimeoutMs, ILogger? logger = null, LegDrainOptions? options = null,
        bool isolateFailures = false)
    {
        if (!db.Database.IsRelational()) return await DrainInMemoryAsync(db, ct);
        options ??= new LegDrainOptions();

        // Revision I1: el tiempo de EF por comando es 30 s; una tanda espera el candado (hasta lockTimeoutMs) y despues
        // calcula. Con 30 s, un comando agotado se deshacia y la misma tanda se repetia sin fin.
        var antes = db.Database.GetCommandTimeout();
        db.Database.SetCommandTimeout(lockTimeoutMs / 1000 + options.BatchCommandSeconds);
        try
        {
            // Revision I2: solo lo encolado hasta ahora. Con altas entrando sin parar, «hasta que no quede nada» podia no
            // terminar nunca; lo que entre mientras tanto lo vacia la siguiente pasada (el trabajo corre cada minuto).
            var hasta = (await db.Database.SqlQueryRaw<long>("SELECT ISNULL(MAX(Id), 0) AS [Value] FROM DualLegDeltas")
                .ToListAsync(ct)).Single();
            if (hasta == 0) return 0;

            var total = 0;
            var sinNodo = 0;
            long ultimo = 0;
            var completa = Math.Max(1, options.WindowRows);
            var ventana = completa;
            // Tercera revision del bloque 2: el error de la tanda de un solo arranque que aun no se confirmo repitiendola.
            int? errorAislado = null;
            for (var i = 0; i < maxBatches; i++)
            {
                BatchResult r;
                try
                {
                    r = await ApplyBatchAsync(db, hasta, ultimo, ventana, options, lockTimeoutMs, ct);
                }
                catch (SqlException ex) when (isolateFailures && DataErrors.Contains(ex.Number)
                                              && db.Database.CurrentTransaction is null && !ct.IsCancellationRequested)
                {
                    // Revision del bloque 2 (M-3) y revision 2 (I1): un fallo de DATOS (un punto que desborda, una clave
                    // repetida) se repite con la mitad de la ventana hasta aislar un arranque; con uno solo, se le anota el
                    // intento y, al llegar a MaxAttempts, sus deltas se apartan para que no bloqueen la cola. Cualquier otro
                    // error (tiempo, conexion, registro, memoria) se propaga sin anotar nada.
                    if (ventana > 1)
                    {
                        logger?.LogWarning(ex, "Cola de piernas: fallo una tanda de {Ventana} filas desde el Id {Ultimo}; se repite con la mitad.", ventana, ultimo);
                        ventana = Math.Max(1, ventana / 2);
                        i--;
                        continue;
                    }
                    // Tercera revision del bloque 2: solo se anota el intento si el MISMO error se repite en la tanda de un
                    // solo arranque. Una clave repetida (2627, 2601) o una FK (547) pueden ser una carrera con otro escritor
                    // que ya paso: la primera vez se repite la tanda aislada sin anotar nada.
                    if (errorAislado != ex.Number)
                    {
                        logger?.LogWarning(ex, "Cola de piernas: fallo la tanda aislada desde el Id {Ultimo} ({Numero}); se repite para confirmarlo.", ultimo, ex.Number);
                        errorAislado = ex.Number;
                        i--;
                        continue;
                    }
                    await RecordFailureAsync(db, hasta, ultimo, ex, lockTimeoutMs, logger, ct);
                    throw;
                }
                total += r.Claimed;
                sinNodo += r.WithoutNode;
                // Revision 2 del bloque 2 (I1): tras una tanda partida que sale bien, la siguiente vuelve a la ventana entera.
                ventana = completa;
                errorAislado = null;
                if (r.WindowRows == 0) break;
                ultimo = r.LastId;
            }
            if (sinNodo > 0)
                logger?.LogWarning("Cola de piernas: {Count} deltas de pierna sin nodo vivo en su arranque apartados a DualLegDeltaDeadLetters (motivo sin-nodo).", sinNodo);
            return total;
        }
        finally
        {
            db.Database.SetCommandTimeout(antes);
        }
    }

    private sealed record BatchResult(int Claimed, int WindowRows, long LastId, int WithoutNode, long PathRows);

    /// <summary>Una tanda en su transaccion, reintentada si es victima de un interbloqueo (revision C2).</summary>
    private static async Task<BatchResult> ApplyBatchAsync(AppDbContext db, long hasta, long ultimo, int ventana,
        LegDrainOptions options, int lockTimeoutMs, CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            var propia = db.Database.CurrentTransaction is null;
            await using var tx = propia ? await db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                await DualTeamGate.EnterLegDrainAsync(db, ct, lockTimeoutMs);
                var conn = (SqlConnection)db.Database.GetDbConnection();
                await using var cmd = new SqlCommand(ApplyBatchSql, conn,
                    (SqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction())
                {
                    CommandTimeout = db.Database.GetCommandTimeout() ?? (lockTimeoutMs / 1000 + options.BatchCommandSeconds)
                };
                cmd.Parameters.Add("@lote", SqlDbType.Int).Value = ventana;
                cmd.Parameters.Add("@maxFilas", SqlDbType.BigInt).Value = (long)options.MaxPathRows;
                cmd.Parameters.Add("@trozo", SqlDbType.Int).Value = options.UpdateChunk;
                cmd.Parameters.Add("@hasta", SqlDbType.BigInt).Value = hasta;
                cmd.Parameters.Add("@ultimo", SqlDbType.BigInt).Value = ultimo;
                BatchResult r;
                await using (var lector = await cmd.ExecuteReaderAsync(ct))
                {
                    if (!await lector.ReadAsync(ct)) throw new InvalidOperationException("La tanda de la cola de piernas no devolvio su resumen.");
                    r = new BatchResult(lector.GetInt32(0), lector.GetInt32(1), lector.GetInt64(2), lector.GetInt32(3), lector.GetInt64(4));
                }
                if (tx is not null) await tx.CommitAsync(ct);
                return r;
            }
            catch (SqlException ex) when (ex.Number == 1205 && propia && intento < MaxDeadlockRetries)
            {
                // SQL Server ya deshizo la tanda; el await using cierra la transaccion y se repite entera.
            }
        }
    }

    /// <summary>
    /// Revision del bloque 2 (M-3): la tanda de un solo arranque fallo. En otra transaccion se anota el intento en sus
    /// deltas y, si llegan a MaxAttempts, se apartan a DualLegDeltaDeadLetters con el error.
    /// </summary>
    private static async Task RecordFailureAsync(AppDbContext db, long hasta, long ultimo, SqlException error, int lockTimeoutMs,
        ILogger? logger, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await DualTeamGate.EnterLegDrainAsync(db, ct, lockTimeoutMs);
            var arranque = (await db.Database.SqlQuery<string>(
                    $"SELECT TOP 1 StartMemberId AS [Value] FROM DualLegDeltas WHERE Id > {ultimo} AND Id <= {hasta} ORDER BY Id")
                .ToListAsync(ct)).SingleOrDefault();
            if (arranque is null) return;
            // Solo se culpa a un arranque que la tanda llego a aplicar (con nodo vivo y clave): uno que espera sin nodo no
            // entra en la tanda, asi que no puede ser el que la hizo fallar.
            var candidato = await db.DualTeamTree.AsNoTracking()
                .AnyAsync(d => d.MemberId == arranque && !d.IsDeleted && d.BinaryPath != null, ct);
            if (!candidato)
            {
                logger?.LogError(error, "Cola de piernas: fallo una tanda de una fila sin arranque aplicable ({StartMemberId}); no se anota intento.", arranque);
                return;
            }
            var motivo = $"fallo: {error.Number} {error.Message}";
            if (motivo.Length > 1000) motivo = motivo[..1000];
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE DualLegDeltas SET Attempts = Attempts + 1 WHERE StartMemberId = {arranque} AND Id <= {hasta}", ct);
            var apartados = await db.Database.ExecuteSqlInterpolatedAsync($@"
DELETE q
OUTPUT deleted.Id, deleted.StartMemberId, deleted.Side, deleted.Points, deleted.NodeCount, deleted.QualifiedCount,
       deleted.OwnChange, deleted.Attempts, deleted.CreationDate, deleted.CreatedBy, GETDATE(), {motivo}
INTO DualLegDeltaDeadLetters (DeltaId, StartMemberId, Side, Points, NodeCount, QualifiedCount, OwnChange, Attempts,
                              CreationDate, CreatedBy, DiscardedDate, Reason)
FROM DualLegDeltas q
WHERE q.StartMemberId = {arranque} AND q.Id <= {hasta}
  AND EXISTS (SELECT 1 FROM DualLegDeltas m WHERE m.StartMemberId = {arranque} AND m.Id <= {hasta} AND m.Attempts >= {MaxAttempts});", ct);
            await tx.CommitAsync(ct);
            if (apartados > 0)
                logger?.LogError(error, "Cola de piernas: los {Count} deltas del arranque {StartMemberId} fallaron {Max} veces y se apartan a DualLegDeltaDeadLetters; sus piernas quedan sin ellos hasta reponerlos.",
                    apartados, arranque, MaxAttempts);
            else
                logger?.LogError(error, "Cola de piernas: fallo la tanda del arranque {StartMemberId}; intento anotado (se aparta al llegar a {Max}).",
                    arranque, MaxAttempts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "Cola de piernas: no se pudo anotar el intento fallido.");
        }
    }

    // Una tanda (revision del bloque 2, I-3):
    //  1. Ventana: las primeras @lote filas con Id en (@ultimo, @hasta], en orden de Id (no se reagrupa la cola entera).
    //  2. Sus arranques. Los deltas DE PIERNA de un arranque sin nodo vivo con clave se apartan a DualLegDeltaDeadLetters
    //     (M-2: antes se descartaban en silencio); los PROPIOS sin nodo esperan (DQ13).
    //  3. Los candidatos, ordenados por la clave (inorden), con C = la profundidad del prefijo comun con el anterior (-1 si
    //     es el primero o de otra raiz). La union de sus caminos son, por cada arranque i, las profundidades (C_i, D_i]: cada
    //     ascendiente distinto sale una sola vez. Si pasa de @maxFilas, se queda la primera mitad por orden de llegada.
    //  4. Reclama (DELETE ... OUTPUT) los deltas de los candidatos con Id <= @hasta y los agrega por arranque UNA vez.
    //  5. Sumas por ascendiente: con R(i,k) = MIN(C_{i+1..k}), los arranques bajo el ascendiente de i a profundidad J son i y
    //     los k > i con R(i,k) >= J. Por el lado de i van los de la profundidad J+1; por el otro, el resto menos el propio
    //     ascendiente si tambien es arranque (R = J y D_k = J). En J = D_i (el propio arranque) todo va a la derecha.
    //  6. Mas lo propio de cada arranque (su pierna Side para los de pierna; SelfQualified para los propios), y se aplica
    //     por trozos de @trozo filas.
    // SET XACT_ABORT ON: cualquier error deshace la tanda entera. DEADLOCK_PRIORITY LOW (I-5): el vaciado es siempre la
    // victima de un interbloqueo (reintenta solo la tanda), no la colocacion ni el trabajo de estadisticas.
    private const string ApplyBatchSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET DEADLOCK_PRIORITY LOW;
DROP TABLE IF EXISTS #ventana; DROP TABLE IF EXISTS #arr; DROP TABLE IF EXISTS #cand; DROP TABLE IF EXISTS #o;
DROP TABLE IF EXISTS #s; DROP TABLE IF EXISTS #lote; DROP TABLE IF EXISTS #t; DROP TABLE IF EXISTS #par;
DROP TABLE IF EXISTS #seg; DROP TABLE IF EXISTS #c; DROP TABLE IF EXISTS #x;

CREATE TABLE #ventana (Id bigint NOT NULL PRIMARY KEY, StartMemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL);
INSERT INTO #ventana (Id, StartMemberId)
SELECT TOP (@lote) q.Id, q.StartMemberId FROM DualLegDeltas q WHERE q.Id > @ultimo AND q.Id <= @hasta ORDER BY q.Id;
DECLARE @enVentana int = @@ROWCOUNT;
DECLARE @nuevoUltimo bigint = ISNULL((SELECT MAX(Id) FROM #ventana), @ultimo);

SELECT v.StartMemberId, MIN(v.Id) AS Primero INTO #arr FROM #ventana v GROUP BY v.StartMemberId;

-- M-2: de pierna sin nodo vivo con clave.
DELETE q
OUTPUT deleted.Id, deleted.StartMemberId, deleted.Side, deleted.Points, deleted.NodeCount, deleted.QualifiedCount,
       deleted.OwnChange, deleted.Attempts, deleted.CreationDate, deleted.CreatedBy, GETDATE(), N'sin-nodo'
INTO DualLegDeltaDeadLetters (DeltaId, StartMemberId, Side, Points, NodeCount, QualifiedCount, OwnChange, Attempts,
                              CreationDate, CreatedBy, DiscardedDate, Reason)
FROM DualLegDeltas q
JOIN #arr a ON a.StartMemberId = q.StartMemberId
WHERE q.Id <= @hasta AND q.OwnChange = 0
  AND NOT EXISTS (SELECT 1 FROM DualTeamTree v WHERE v.MemberId = q.StartMemberId AND v.IsDeleted = 0 AND v.BinaryPath IS NOT NULL);
DECLARE @sinNodo int = @@ROWCOUNT;

SELECT a.StartMemberId, a.Primero, d.Id AS NodeId, d.BinaryPath, z.D
INTO #cand
FROM #arr a
JOIN DualTeamTree d ON d.MemberId = a.StartMemberId AND d.IsDeleted = 0 AND d.BinaryPath IS NOT NULL
CROSS APPLY dbo.DualKeyDepth(d.BinaryPath) z;

-- 3. Orden por la clave, prefijo comun con el anterior y tamano de la union; la mitad si no cabe.
CREATE TABLE #o (K bigint NOT NULL PRIMARY KEY, StartMemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL,
                 Primero bigint NOT NULL, NodeId nvarchar(900) COLLATE DATABASE_DEFAULT NOT NULL,
                 BinaryPath varbinary(1700) NOT NULL, D int NOT NULL);
CREATE TABLE #s (K bigint NOT NULL PRIMARY KEY, StartMemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL,
                 Primero bigint NOT NULL, NodeId nvarchar(900) COLLATE DATABASE_DEFAULT NOT NULL,
                 BinaryPath varbinary(1700) NOT NULL, D int NOT NULL, C int NOT NULL);
DECLARE @tope bigint = (SELECT MAX(Primero) FROM #cand), @filas bigint = 0, @n int;
WHILE @tope IS NOT NULL
BEGIN
    TRUNCATE TABLE #o; TRUNCATE TABLE #s;
    INSERT INTO #o (K, StartMemberId, Primero, NodeId, BinaryPath, D)
    SELECT ROW_NUMBER() OVER (ORDER BY c.BinaryPath), c.StartMemberId, c.Primero, c.NodeId, c.BinaryPath, c.D
    FROM #cand c WHERE c.Primero <= @tope;

    INSERT INTO #s (StartMemberId, Primero, NodeId, BinaryPath, D, K, C)
    SELECT o.StartMemberId, o.Primero, o.NodeId, o.BinaryPath, o.D, o.K,
           CASE WHEN p.K IS NULL THEN -1
                WHEN y.Pos IS NULL THEN CASE WHEN o.D < p.D THEN o.D ELSE p.D END
                WHEN y.Pos <= 4 THEN -1
                ELSE (SELECT MIN(m.v) FROM (VALUES ((y.Pos - 5) * 8 + y.Clz), (o.D), (p.D)) m(v)) END AS C
    FROM #o o
    LEFT JOIN #o p ON p.K = o.K - 1
    OUTER APPLY (SELECT MIN(n.N) AS T
                 FROM dbo.NumerosEntre(0, (CASE WHEN DATALENGTH(o.BinaryPath) > DATALENGTH(p.BinaryPath)
                                                THEN DATALENGTH(o.BinaryPath) ELSE DATALENGTH(p.BinaryPath) END - 1) / 64) n
                 WHERE p.K IS NOT NULL
                   AND SUBSTRING(o.BinaryPath, n.N * 64 + 1, 64) <> SUBSTRING(p.BinaryPath, n.N * 64 + 1, 64)) t
    OUTER APPLY (SELECT TOP 1 t.T * 64 + m.N AS Pos,
                        CASE WHEN b.X >= 128 THEN 0 WHEN b.X >= 64 THEN 1 WHEN b.X >= 32 THEN 2 WHEN b.X >= 16 THEN 3
                             WHEN b.X >= 8 THEN 4 WHEN b.X >= 4 THEN 5 WHEN b.X >= 2 THEN 6 ELSE 7 END AS Clz
                 FROM dbo.NumerosEntre(1, 64) m
                 CROSS APPLY (SELECT CAST(SUBSTRING(o.BinaryPath, t.T * 64 + m.N, 1) AS int)
                                     ^ CAST(SUBSTRING(p.BinaryPath, t.T * 64 + m.N, 1) AS int) AS X) b
                 WHERE t.T IS NOT NULL AND b.X <> 0
                 ORDER BY m.N) y
    OPTION (MAXDOP 1);

    SELECT @filas = SUM(CAST(D - C AS bigint)), @n = COUNT(*) FROM #s;
    IF @filas <= @maxFilas OR @n <= 1 BREAK;
    SET @tope = (SELECT MAX(h.Primero) FROM (SELECT TOP (@n / 2) Primero FROM #s ORDER BY Primero) h);
END
IF @tope IS NOT NULL AND EXISTS (SELECT 1 FROM #cand WHERE Primero > @tope)
    -- Los que se quedan fuera se ven en la siguiente tanda: la marca de agua vuelve a justo antes del primero.
    SET @nuevoUltimo = (SELECT MIN(Primero) FROM #cand WHERE Primero > @tope) - 1;

-- 4. Reclamar y agregar una vez por arranque.
CREATE TABLE #lote (StartMemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL, Side int NOT NULL,
                    Points decimal(18,4) NOT NULL, NodeCount int NOT NULL, QualifiedCount int NOT NULL, OwnChange bit NOT NULL);
DELETE q
OUTPUT deleted.StartMemberId, deleted.Side, deleted.Points, deleted.NodeCount, deleted.QualifiedCount, deleted.OwnChange INTO #lote
FROM DualLegDeltas q
JOIN #s s ON s.StartMemberId = q.StartMemberId
WHERE q.Id <= @hasta;
DECLARE @reclamados int = @@ROWCOUNT;

-- T*: lo que sube a los ascendientes (todo); S*: lo propio del arranque. Un propio va a SelfQualified y a las piernas de
-- los ascendientes, nunca a una pierna del propio arranque.
SELECT s.K, s.NodeId, s.D, s.C, s.BinaryPath,
       SUM(l.Points) AS TP, SUM(l.NodeCount) AS TN, SUM(l.QualifiedCount) AS TQ,
       SUM(CASE WHEN l.OwnChange = 0 AND l.Side = 0 THEN l.Points ELSE 0 END) AS SLP,
       SUM(CASE WHEN l.OwnChange = 0 AND l.Side = 1 THEN l.Points ELSE 0 END) AS SRP,
       SUM(CASE WHEN l.OwnChange = 0 AND l.Side = 0 THEN l.NodeCount ELSE 0 END) AS SLC,
       SUM(CASE WHEN l.OwnChange = 0 AND l.Side = 1 THEN l.NodeCount ELSE 0 END) AS SRC,
       SUM(CASE WHEN l.OwnChange = 0 AND l.Side = 0 THEN l.QualifiedCount ELSE 0 END) AS SLQ,
       SUM(CASE WHEN l.OwnChange = 0 AND l.Side = 1 THEN l.QualifiedCount ELSE 0 END) AS SRQ,
       SUM(CASE WHEN l.OwnChange = 1 THEN l.QualifiedCount ELSE 0 END) AS SSQ
INTO #t
FROM #s s JOIN #lote l ON l.StartMemberId = s.StartMemberId
GROUP BY s.K, s.NodeId, s.D, s.C, s.BinaryPath;
CREATE UNIQUE CLUSTERED INDEX ct ON #t (K);

-- 5. Pares (i, k) mientras el prefijo comun con i siga por debajo de C_i, y R(i,k) = MIN(C) de i+1 a k.
SELECT a.K AS I, b.K, b.D, b.TP, b.TN, b.TQ,
       MIN(b.C) OVER (PARTITION BY a.K ORDER BY b.K ROWS UNBOUNDED PRECEDING) AS R
INTO #par
FROM #t a
JOIN #t b ON b.K > a.K
WHERE b.K < ISNULL((SELECT MIN(c.K) FROM #t c WHERE c.K > a.K AND c.C <= a.C), 2147483647)
OPTION (MAXDOP 1);

SELECT g.I, g.R,
       SUM(g.GP) OVER (PARTITION BY g.I ORDER BY g.R DESC ROWS UNBOUNDED PRECEDING) AS SufP,
       SUM(g.GN) OVER (PARTITION BY g.I ORDER BY g.R DESC ROWS UNBOUNDED PRECEDING) AS SufN,
       SUM(g.GQ) OVER (PARTITION BY g.I ORDER BY g.R DESC ROWS UNBOUNDED PRECEDING) AS SufQ,
       g.XP, g.XN, g.XQ
INTO #seg
FROM (SELECT p.I, p.R, SUM(p.TP) AS GP, SUM(p.TN) AS GN, SUM(p.TQ) AS GQ,
             SUM(CASE WHEN p.D = p.R THEN p.TP ELSE 0 END) AS XP,
             SUM(CASE WHEN p.D = p.R THEN p.TN ELSE 0 END) AS XN,
             SUM(CASE WHEN p.D = p.R THEN p.TQ ELSE 0 END) AS XQ
      FROM #par p GROUP BY p.I, p.R) g;
CREATE UNIQUE CLUSTERED INDEX cg ON #seg (I, R);

-- La union de los caminos: (C_i, D_i] de cada arranque, con lo que recibe cada ascendiente por cada lado.
SELECT CASE WHEN a.IsSelf = 1 THEN t.NodeId END AS SelfNode, a.AncestorKey, a.IsSelf, a.Side,
       t.TP + ISNULL(j0.SufP, 0) AS P0, t.TN + ISNULL(j0.SufN, 0) AS N0, t.TQ + ISNULL(j0.SufQ, 0) AS Q0,
       t.TP + ISNULL(j1.SufP, 0) AS P1, t.TN + ISNULL(j1.SufN, 0) AS N1, t.TQ + ISNULL(j1.SufQ, 0) AS Q1,
       ISNULL(x.XP, 0) AS XP, ISNULL(x.XN, 0) AS XN, ISNULL(x.XQ, 0) AS XQ, t.TP, t.TN, t.TQ
INTO #c
FROM #t t
CROSS APPLY dbo.DualPathKeys(t.BinaryPath, 0, t.C + 1) a
OUTER APPLY (SELECT TOP 1 g.SufP, g.SufN, g.SufQ FROM #seg g WHERE g.I = t.K AND g.R >= a.Depth ORDER BY g.R) j0
OUTER APPLY (SELECT TOP 1 g.SufP, g.SufN, g.SufQ FROM #seg g WHERE g.I = t.K AND g.R >= a.Depth + 1 ORDER BY g.R) j1
LEFT JOIN #seg x ON x.I = t.K AND x.R = a.Depth
OPTION (MAXDOP 1);

-- 6. Por nodo: los ascendientes (por la clave; el propio arranque, por su Id) y lo propio de cada arranque.
SELECT d.Id AS NodeId, d.MemberId,
       SUM(u.LP) AS LP, SUM(u.RP) AS RP, SUM(u.LC) AS LC, SUM(u.RC) AS RC, SUM(u.LQ) AS LQ, SUM(u.RQ) AS RQ, SUM(u.SQ) AS SQ,
       ROW_NUMBER() OVER (ORDER BY d.Id) AS Rn
INTO #x
FROM (
    SELECT ISNULL(c.SelfNode, k.Id) AS NodeId,
           CASE WHEN c.IsSelf = 1 THEN 0 WHEN c.Side = 0 THEN c.P1 ELSE c.P0 - c.P1 - c.XP END AS LP,
           CASE WHEN c.IsSelf = 1 THEN c.P0 - c.TP WHEN c.Side = 1 THEN c.P1 ELSE c.P0 - c.P1 - c.XP END AS RP,
           CASE WHEN c.IsSelf = 1 THEN 0 WHEN c.Side = 0 THEN c.N1 ELSE c.N0 - c.N1 - c.XN END AS LC,
           CASE WHEN c.IsSelf = 1 THEN c.N0 - c.TN WHEN c.Side = 1 THEN c.N1 ELSE c.N0 - c.N1 - c.XN END AS RC,
           CASE WHEN c.IsSelf = 1 THEN 0 WHEN c.Side = 0 THEN c.Q1 ELSE c.Q0 - c.Q1 - c.XQ END AS LQ,
           CASE WHEN c.IsSelf = 1 THEN c.Q0 - c.TQ WHEN c.Side = 1 THEN c.Q1 ELSE c.Q0 - c.Q1 - c.XQ END AS RQ,
           0 AS SQ
    FROM #c c
    LEFT JOIN DualTeamTree k ON c.SelfNode IS NULL AND k.BinaryPath = c.AncestorKey AND k.IsDeleted = 0
    WHERE c.SelfNode IS NOT NULL OR k.Id IS NOT NULL
    UNION ALL
    SELECT t.NodeId, t.SLP, t.SRP, t.SLC, t.SRC, t.SLQ, t.SRQ, t.SSQ FROM #t t
) u
JOIN DualTeamTree d ON d.Id = u.NodeId
GROUP BY d.Id, d.MemberId
OPTION (MAXDOP 1);
CREATE CLUSTERED INDEX cx ON #x (Rn);

DECLARE @desde int = 1, @nx int = (SELECT COUNT(*) FROM #x);
WHILE @desde <= @nx
BEGIN
    UPDATE d
    SET LeftLegPoints     = d.LeftLegPoints     + x.LP,
        RightLegPoints    = d.RightLegPoints    + x.RP,
        LeftLegCount      = d.LeftLegCount      + x.LC,
        RightLegCount     = d.RightLegCount     + x.RC,
        LeftLegQualified  = d.LeftLegQualified  + x.LQ,
        RightLegQualified = d.RightLegQualified + x.RQ,
        SelfQualified     = d.SelfQualified     + x.SQ
    FROM DualTeamTree d
    JOIN #x x ON x.NodeId = d.Id
    WHERE x.Rn >= @desde AND x.Rn < @desde + @trozo;

    -- DQ15: la copia del tamano del equipo binario en las estadisticas, junto a la de los puntos.
    UPDATE s
    SET DualTeamPoints = CAST(d.LeftLegPoints + d.RightLegPoints AS int),
        DualTeamSize   = d.LeftLegCount + d.RightLegCount
    FROM MemberStatistics s
    JOIN #x x ON x.MemberId = s.MemberId AND x.Rn >= @desde AND x.Rn < @desde + @trozo
    JOIN DualTeamTree d ON d.Id = x.NodeId;

    SET @desde += @trozo;
END

SELECT @reclamados AS Reclamados, @enVentana AS Ventana, @nuevoUltimo AS Ultimo, @sinNodo AS SinNodo,
       ISNULL(@filas, 0) AS Filas;
DROP TABLE IF EXISTS #x; DROP TABLE IF EXISTS #c; DROP TABLE IF EXISTS #seg; DROP TABLE IF EXISTS #par; DROP TABLE IF EXISTS #t;
DROP TABLE IF EXISTS #lote; DROP TABLE IF EXISTS #s; DROP TABLE IF EXISTS #o; DROP TABLE IF EXISTS #cand; DROP TABLE IF EXISTS #arr;
DROP TABLE IF EXISTS #ventana;";

    /// <summary>
    /// Revision del bloque 2 (M-5): en InMemory la cola solo guarda propios (los de pierna se aplican en el acto). Se
    /// aplican con la misma semantica que en SQL: los propios con nodo vivo a SelfQualified y a las piernas de sus
    /// ascendientes; sin nodo, esperan. Un delta de pierna (sembrado a mano) va a la pierna Side del arranque y sube.
    /// </summary>
    private static async Task<int> DrainInMemoryAsync(AppDbContext db, CancellationToken ct)
    {
        var pendientes = await db.DualLegDeltas.OrderBy(d => d.Id).ToListAsync(ct);
        var aplicados = 0;
        foreach (var q in pendientes)
        {
            var nodo = await db.DualTeamTree.FirstOrDefaultAsync(d => d.MemberId == q.StartMemberId && !d.IsDeleted, ct);
            if (nodo is null)
            {
                if (q.OwnChange) continue;
                db.DualLegDeltaDeadLetters.Add(new DualLegDeltaDeadLetter
                {
                    DeltaId = q.Id, StartMemberId = q.StartMemberId, Side = q.Side, Points = q.Points, NodeCount = q.NodeCount,
                    QualifiedCount = q.QualifiedCount, OwnChange = q.OwnChange, Attempts = q.Attempts, CreationDate = q.CreationDate,
                    CreatedBy = q.CreatedBy, DiscardedDate = DateTime.Now, Reason = "sin-nodo"
                });
                db.DualLegDeltas.Remove(q);
                continue;
            }
            if (q.OwnChange) nodo.SelfQualified += q.QualifiedCount;
            else AddToLeg(nodo, q.Side, q.Points, q.NodeCount, q.QualifiedCount);
            await RefreshStatsInMemoryAsync(db, nodo, ct);
            await AddUpInMemoryAsync(db, nodo, q.Points, q.NodeCount, q.QualifiedCount, ct);
            db.DualLegDeltas.Remove(q);
            aplicados++;
        }
        await db.SaveChangesAsync(ct);
        return aplicados;
    }

    /// <summary>
    /// La misma semantica sobre el proveedor InMemory (pruebas de logica): sube nodo a nodo. Revision del bloque 2 (M-5):
    /// tambien los calificados (la hoja, 0; un subarbol, SelfQualified + sus piernas) y DualTeamSize.
    /// </summary>
    private static async Task ApplyInMemoryAsync(AppDbContext db, string rootId, bool leaf, int sign, CancellationToken ct)
    {
        var node = await db.DualTeamTree.FirstOrDefaultAsync(d => d.MemberId == rootId, ct);
        if (node is null) return;
        var personal = (await db.MemberStatistics.Where(s => s.MemberId == rootId).OrderBy(s => s.Id)
            .Select(s => (decimal?)s.PersonalPoints).FirstOrDefaultAsync(ct) ?? 0m)
            - await db.DualLegDeltas.Where(q => q.StartMemberId == rootId && q.OwnChange).SumAsync(q => q.Points, ct);
        var pts = sign * (leaf ? personal : personal + node.LeftLegPoints + node.RightLegPoints);
        var cnt = sign * (leaf ? 1 : 1 + node.LeftLegCount + node.RightLegCount);
        var cal = sign * (leaf ? 0 : node.SelfQualified + node.LeftLegQualified + node.RightLegQualified);
        await AddUpInMemoryAsync(db, node, pts, cnt, cal, ct);
        await db.SaveChangesAsync(ct);
    }

    private static void AddToLeg(DualTeamEntity n, TreeSide side, decimal pts, int cnt, int cal)
    {
        if (side == TreeSide.Left) { n.LeftLegPoints += pts; n.LeftLegCount += cnt; n.LeftLegQualified += cal; }
        else { n.RightLegPoints += pts; n.RightLegCount += cnt; n.RightLegQualified += cal; }
    }

    private static async Task RefreshStatsInMemoryAsync(AppDbContext db, DualTeamEntity n, CancellationToken ct)
    {
        var stats = await db.MemberStatistics.Where(s => s.MemberId == n.MemberId).OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (stats is null) return;
        stats.DualTeamPoints = (int)(n.LeftLegPoints + n.RightLegPoints);
        stats.DualTeamSize = n.LeftLegCount + n.RightLegCount;
    }

    /// <summary>Suma a cada ascendiente de <paramref name="from"/>, por el lado por el que se baja (por ParentMemberId).</summary>
    private static async Task AddUpInMemoryAsync(AppDbContext db, DualTeamEntity from, decimal pts, int cnt, int cal, CancellationToken ct)
    {
        var actual = from;
        var visitados = new HashSet<string> { actual.MemberId };
        while (actual.ParentMemberId is not null)
        {
            var padre = await db.DualTeamTree.FirstOrDefaultAsync(d => d.MemberId == actual.ParentMemberId, ct);
            if (padre is null || !visitados.Add(padre.MemberId)) break;
            AddToLeg(padre, actual.Side, pts, cnt, cal);
            await RefreshStatsInMemoryAsync(db, padre, ct);
            actual = padre;
        }
    }
}
