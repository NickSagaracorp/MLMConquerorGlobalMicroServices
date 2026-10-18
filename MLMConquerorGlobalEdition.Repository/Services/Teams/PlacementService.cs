using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

public record PlacementOutcome(string MemberId, string ParentMemberId, TreeSide Side, int Depth, bool AlreadyPlaced);
public record BulkPlacementOutcome(int Placed, int Skipped, int Failed);

/// <summary>Quien firma la colocacion: CreatedBy del nodo y del log, y la accion y el motivo del log.</summary>
public sealed record PlacementAudit(string CreatedBy, string Action, string Reason)
{
    public static readonly PlacementAudit Engine = new("placement-service", "Placed", "Placement engine");
}

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
/// so step (c) is O(1). The cache self-heals if stale (one recursive query over the leg).
/// Concurrency (Plan P): every placement runs in a transaction, takes <see cref="DualTeamGate"/>
/// on the TARGET slot, re-checks the slot inside the lock and retries if another writer got
/// there first. UX_DualTeamTree_ParentMemberId_Side and IX_DualTeamTree_MemberId are the hard
/// backstops (a unique violation also retries). Leg points are maintained incrementally.
/// </summary>
public interface IPlacementService
{
    /// <summary>Place one member. Transactional, idempotent, concurrency-safe. Maintains leg points.</summary>
    Task<Result<PlacementOutcome>> PlaceAsync(string memberId, string sponsorMemberId, CancellationToken ct = default);

    /// <summary>Same as <see cref="PlaceAsync(string,string,CancellationToken)"/>, signed by <paramref name="audit"/>.</summary>
    Task<Result<PlacementOutcome>> PlaceAsync(string memberId, string sponsorMemberId, PlacementAudit audit, CancellationToken ct = default);

    /// <summary>
    /// Plan P: places one by one through <see cref="PlaceAsync(string,string,CancellationToken)"/>; safe under
    /// concurrency. Each member commits on its own, so a failure does not undo the members already placed.
    /// </summary>
    Task<Result<BulkPlacementOutcome>> PlaceBulkAsync(
        IReadOnlyList<(string MemberId, string SponsorMemberId)> pairs, CancellationToken ct = default);
}

public class PlacementService : IPlacementService
{
    /// <summary>
    /// Intentos cuando otro escritor ocupa el hueco calculado (o gana la carrera del indice). Los derrames de
    /// muchos patrocinadores caen en la misma cola de cadena: con N escritores simultaneos sobre ella, en cada
    /// ronda gana uno, asi que un perdedor puede necesitar hasta N-1 rondas. 50 cubre de sobra la concurrencia
    /// real (AutoPlacementJob es secuencial) y la prueba de 40 escritores.
    /// </summary>
    private const int MaxIntentos = 50;

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

    public Task<Result<PlacementOutcome>> PlaceAsync(
        string memberId, string sponsorMemberId, CancellationToken ct = default)
        => PlaceAsync(memberId, sponsorMemberId, PlacementAudit.Engine, ct);

    public async Task<Result<PlacementOutcome>> PlaceAsync(
        string memberId, string sponsorMemberId, PlacementAudit audit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(memberId))
            return Result<PlacementOutcome>.Failure("INVALID_MEMBER", "memberId is required.");
        if (string.IsNullOrWhiteSpace(sponsorMemberId))
            return Result<PlacementOutcome>.Failure("INVALID_SPONSOR", "sponsorMemberId is required.");

        for (var intento = 1; intento <= MaxIntentos; intento++)
        {
            var existing = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
            if (existing is not null)
                return Result<PlacementOutcome>.Success(new PlacementOutcome(
                    memberId, existing.ParentMemberId ?? "", existing.Side, Depth(existing.HierarchyPath), true));

            await using var scope = await PlacementScope.BeginAsync(_db, ct);
            try
            {
                var target = await ResolveTargetAsync(sponsorMemberId, audit, scope, ct);

                // Plan P: el candado es del HUECO destino (antes, del patrocinador: dos patrocinadores que
                // derramaban al mismo nodo no se esperaban). Dentro se vuelve a comprobar todo.
                await DualTeamGate.EnterSlotAsync(_db, target.ParentId, target.Side, ct);
                if (await _db.DualTeamTree.AnyAsync(d => d.MemberId == memberId, ct)
                    || await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == target.ParentId && d.Side == target.Side, ct))
                {
                    await scope.UndoAsync(ct);
                    await EsperaAntesDeReintentarAsync(scope, ct);
                    continue;   // otro escritor llego antes: se recalcula el hueco
                }

                var node = NewNode(memberId, target.ParentId, target.Side, target.ParentPath, audit.CreatedBy);
                scope.Track(_db.DualTeamTree.Add(node).Entity);
                scope.Track(_db.PlacementLogs.Add(NewLog(memberId, target.ParentId, target.Side, audit)).Entity);
                await UpsertFrontierAsync(sponsorMemberId, target, memberId, scope, ct);
                await _db.SaveChangesAsync(ct);

                // Subida incremental de puntos de pierna para la hoja nueva, dentro de la misma transaccion.
                // Es SQL de SQL Server: en InMemory (pruebas de logica) no hay piernas que mantener.
                if (_db.Database.IsRelational())
                    await LegPointMaintenance.ApplyIncrementalAsync(_db, new[] { memberId }, ct);

                await scope.CompleteAsync(ct);
                return Result<PlacementOutcome>.Success(new PlacementOutcome(
                    memberId, target.ParentId, target.Side, Depth(node.HierarchyPath), false));
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await scope.UndoAsync(ct);   // la red del indice (hueco, miembro o cola): se recalcula
                await EsperaAntesDeReintentarAsync(scope, ct);
            }
            catch (SqlException ex) when (ex.Number == 1205 && scope.OwnsTransaction)
            {
                await scope.UndoAsync(ct);   // victima de un interbloqueo: se repite entera
                await EsperaAntesDeReintentarAsync(scope, ct);
            }
            catch (DualTeamBusyException ex)
            {
                await scope.UndoAsync(ct);
                return Result<PlacementOutcome>.Failure("PLACEMENT_BUSY", ex.Message);
            }
            catch (Exception ex)
            {
                await scope.UndoAsync(ct);
                return Result<PlacementOutcome>.Failure("PLACEMENT_FAILED", ex.Message);
            }
        }
        return Result<PlacementOutcome>.Failure("PLACEMENT_CONFLICT", "The slot kept changing; retry later.");
    }

    /// <summary>Unos milisegundos al azar para que los perdedores no vuelvan todos a la vez al mismo hueco.</summary>
    private static Task EsperaAntesDeReintentarAsync(PlacementScope scope, CancellationToken ct) =>
        scope.OwnsTransaction ? Task.Delay(Random.Shared.Next(2, 20), ct) : Task.CompletedTask;

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException s && s.Number is 2601 or 2627;

    private sealed record Target(string ParentId, TreeSide Side, string ParentPath, bool IsSpill, TreeSide PreferredSide);

    /// <summary>Resolve the placement slot for a sponsor by rules a/b/c (queries the live tree).</summary>
    private async Task<Target> ResolveTargetAsync(
        string sponsorMemberId, PlacementAudit audit, PlacementScope scope, CancellationToken ct)
    {
        var sponsorNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == sponsorMemberId, ct);

        if (sponsorNode is null)
        {
            // Sponsor isn't in the tree yet → create it as a root, then place under it (Left).
            // Dos colocaciones que crean la misma raiz chocan en IX_DualTeamTree_MemberId y la perdedora reintenta.
            var root = NewNode(sponsorMemberId, null, TreeSide.Left, null, audit.CreatedBy);
            scope.Track(_db.DualTeamTree.Add(root).Entity);
            await _db.SaveChangesAsync(ct);
            return new Target(sponsorMemberId, TreeSide.Left, root.HierarchyPath, false, TreeSide.Left);
        }

        var preferred = DualTeamPlacementRules.PreferredSide(sponsorNode.ParentMemberId is null, sponsorNode.Side);

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
            // Rule c — spill to the deepest open node on the preferred side. Plan P: siempre se resuelve
            // la cola y su lado libre sale de la BD (la ruta masiva anterior lo fijaba en false).
            (deepId, deepPath) = await ResolveDeepestAsync(sponsorMemberId, preferred, ct);
            deepHasLeft = await _db.DualTeamTree.AsNoTracking()
                .AnyAsync(d => d.ParentMemberId == deepId && d.Side == TreeSide.Left, ct);
        }

        var decision = DualTeamPlacementRules.Decide(sponsorMemberId, hasLeft, hasRight, deepId, deepHasLeft);
        var parentPath = decision.IsSpill ? deepPath : sponsorNode.HierarchyPath;
        return new Target(decision.ParentMemberId, decision.Side, parentPath, decision.IsSpill, preferred);
    }

    /// <summary>
    /// Deepest open node on the sponsor's preferred side. Reads the cached frontier first (O(1));
    /// if there is no cache or it is stale, one recursive query over the leg (Plan P: before, a BFS
    /// with one query per node). Ties by (CreationDate, Id), deterministic.
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
            // La cola cacheada vale si sigue en el arbol y tiene un hueco libre. Su lado libre lo decide
            // quien llama leyendo la BD, asi que una cola con hijo izquierdo recibe el derecho.
            if (cached is not null
                && await _db.DualTeamTree.CountAsync(d => d.ParentMemberId == cached.MemberId, ct) < 2)
                return (cached.MemberId, cached.HierarchyPath);
        }

        string? id;
        if (_db.Database.IsSqlServer())
        {
            var lado = (int)preferred;
            var filas = await _db.Database.SqlQuery<string>($"""
                WITH s AS (
                    SELECT d.MemberId, 1 AS D, d.CreationDate, d.Id FROM DualTeamTree d
                    WHERE d.ParentMemberId = {sponsorMemberId} AND d.Side = {lado} AND d.IsDeleted = 0
                    UNION ALL
                    SELECT c.MemberId, s.D + 1, c.CreationDate, c.Id FROM s
                    JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0)
                SELECT TOP 1 s.MemberId AS [Value] FROM s
                WHERE (SELECT COUNT(*) FROM DualTeamTree k WHERE k.ParentMemberId = s.MemberId AND k.IsDeleted = 0) < 2
                ORDER BY s.D DESC, s.CreationDate, s.Id
                OPTION (MAXRECURSION 32767)
                """).ToListAsync(ct);
            id = filas.Count == 1 ? filas[0] : null;
        }
        else
        {
            id = await DeepestOpenNodeInMemoryAsync(sponsorMemberId, preferred, ct);
        }

        // Una pierna con hijo siempre tiene una hoja: si no aparece nada, el arbol esta corrupto (por
        // ejemplo, un ciclo) y se falla en vez de inventar un destino.
        if (id is null)
            throw new InvalidOperationException($"No open node found on the {preferred} leg of {sponsorMemberId}.");
        var n = await _db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == id, ct);
        return (n.MemberId, n.HierarchyPath);
    }

    /// <summary>
    /// La misma busqueda en memoria, solo para el proveedor InMemory de las pruebas de logica (no tiene
    /// SQL): recorrido por niveles con el mismo criterio (mas profundo y luego (CreationDate, Id)).
    /// </summary>
    private async Task<string?> DeepestOpenNodeInMemoryAsync(string sponsorMemberId, TreeSide preferred, CancellationToken ct)
    {
        var nivel = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == sponsorMemberId && d.Side == preferred)
            .Select(d => new { d.MemberId, d.CreationDate, d.Id }).ToListAsync(ct);
        string? best = null;
        while (nivel.Count > 0)
        {
            var ids = nivel.Select(n => n.MemberId).ToList();
            var hijos = await _db.DualTeamTree.AsNoTracking()
                .Where(d => d.ParentMemberId != null && ids.Contains(d.ParentMemberId))
                .Select(d => new { d.MemberId, d.ParentMemberId, d.CreationDate, d.Id }).ToListAsync(ct);
            var abiertos = nivel.Where(n => hijos.Count(h => h.ParentMemberId == n.MemberId) < 2)
                .OrderBy(n => n.CreationDate).ThenBy(n => n.Id, StringComparer.Ordinal).ToList();
            if (abiertos.Count > 0) best = abiertos[0].MemberId;
            nivel = hijos.Select(h => new { h.MemberId, h.CreationDate, h.Id }).ToList();
        }
        return best;
    }

    private async Task UpsertFrontierAsync(
        string sponsorMemberId, Target target, string newMemberId, PlacementScope scope, CancellationToken ct)
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
            scope.Track(_db.DualTeamLegFrontiers.Add(new DualTeamLegFrontier
            {
                SponsorMemberId = sponsorMemberId,
                PreferredSide = target.PreferredSide,
                DeepestMemberId = newMemberId,
                DeepestDepth = newDepth
            }).Entity);
        }
        else
        {
            scope.Track(frontier);
            if (newDepth >= frontier.DeepestDepth)
            {
                frontier.PreferredSide = target.PreferredSide;
                frontier.DeepestMemberId = newMemberId;
                frontier.DeepestDepth = newDepth;
            }
        }
    }

    private DualTeamEntity NewNode(string memberId, string? parentId, TreeSide side, string? parentPath, string createdBy)
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
            CreatedBy = createdBy,
            LastUpdateDate = now,
            LastUpdateBy = createdBy
        };
    }

    private PlacementLog NewLog(string memberId, string parentId, TreeSide side, PlacementAudit audit)
    {
        var now = _clock.Now;
        return new PlacementLog
        {
            MemberId = memberId,
            PlacedUnderMemberId = parentId,
            Side = side,
            Action = audit.Action,
            Reason = audit.Reason,
            UnplacementCount = 0,
            FirstPlacementDate = now,
            CreationDate = now,
            CreatedBy = audit.CreatedBy
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Bulk path
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plan P: cada miembro pasa por el mismo nucleo que PlaceAsync (transaccion, candado del hueco,
    /// comprobacion dentro y reintento). La version anterior guardaba un estado en memoria por patrocinador
    /// que no veia lo anadido en la tanda y repetia huecos (diagnostico-huecos-binario.md §1.2).
    /// </summary>
    public async Task<Result<BulkPlacementOutcome>> PlaceBulkAsync(
        IReadOnlyList<(string MemberId, string SponsorMemberId)> pairs, CancellationToken ct = default)
    {
        if (pairs is null || pairs.Count == 0)
            return Result<BulkPlacementOutcome>.Success(new BulkPlacementOutcome(0, 0, 0));

        int placed = 0, skipped = 0, failed = 0;
        foreach (var (memberId, sponsorId) in pairs.DistinctBy(p => p.MemberId))
        {
            var r = await PlaceAsync(memberId, sponsorId, ct);
            if (!r.IsSuccess) failed++;
            else if (r.Value!.AlreadyPlaced) skipped++;
            else placed++;
        }
        return Result<BulkPlacementOutcome>.Success(new BulkPlacementOutcome(placed, skipped, failed));
    }

    /// <summary>
    /// La transaccion de una colocacion. Si quien llama ya tiene una abierta (una prueba que lo deshace todo,
    /// o un manejador que coloca dentro de la suya), se trabaja dentro con un punto de guardado; si no, se
    /// abre una propia. Deshacer suelta del seguimiento las entidades que anadio esta colocacion, sin tocar
    /// las de quien llama; al terminar con exito tambien se sueltan, para que una tanda larga no acumule
    /// miles de entidades en el contexto.
    /// </summary>
    private sealed class PlacementScope : IAsyncDisposable
    {
        private const string Savepoint = "dual_team_place";
        private readonly AppDbContext _db;
        private readonly IDbContextTransaction? _own;
        private readonly bool _savepoint;
        private readonly List<object> _tracked = new();
        private bool _done;

        private PlacementScope(AppDbContext db, IDbContextTransaction? own, bool savepoint)
        {
            _db = db; _own = own; _savepoint = savepoint;
        }

        public bool OwnsTransaction => _own is not null;

        public static async Task<PlacementScope> BeginAsync(AppDbContext db, CancellationToken ct)
        {
            var ambient = db.Database.CurrentTransaction;
            if (ambient is null)
                return new PlacementScope(db, await db.Database.BeginTransactionAsync(ct), false);
            var savepoint = db.Database.IsRelational() && ambient.SupportsSavepoints;
            if (savepoint) await ambient.CreateSavepointAsync(Savepoint, ct);
            return new PlacementScope(db, null, savepoint);
        }

        public void Track(object entity) => _tracked.Add(entity);

        public async Task CompleteAsync(CancellationToken ct)
        {
            if (_own is not null) await _own.CommitAsync(ct);
            _done = true;
            Detach();
        }

        public async Task UndoAsync(CancellationToken ct)
        {
            if (_done) return;
            _done = true;
            try
            {
                if (_own is not null) await _own.RollbackAsync(CancellationToken.None);
                else if (_savepoint) await _db.Database.CurrentTransaction!.RollbackToSavepointAsync(Savepoint, CancellationToken.None);
            }
            catch (Exception) when (_own is not null)
            {
                // Victima de un interbloqueo o conexion caida: SQL Server ya descarto la transaccion.
            }
            Detach();
        }

        private void Detach()
        {
            foreach (var e in _tracked) _db.Entry(e).State = EntityState.Detached;
            _tracked.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_done) await UndoAsync(CancellationToken.None);
            if (_own is not null) await _own.DisposeAsync();
        }
    }
}
