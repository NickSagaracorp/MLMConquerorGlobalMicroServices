using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
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
/// The single authority for placing members into the dual-team binary tree.
///
/// Placement rules (deepest-chain spillover):
///   a) sponsor has no left child   → place LEFT under sponsor
///   b) sponsor has left, no right  → place RIGHT under sponsor
///   c) sponsor has both children   → spill to the DEEPEST open node on the sponsor's
///      preferred side (the sponsor's own Side; Left for a root) and place there.
///
/// Provisional roots (decision de Nick, 2026-10-18): a member whose sponsor is not in the tree yet can
/// already build a team; the engine creates the sponsor as a ROOT (provisional) and places under it. When
/// that provisional root is itself placed, its whole subtree MOVES, unchanged inside, to the slot the rules
/// give under its sponsor, and its accumulated volume rises to the new upline. A root with no sponsor (a
/// system root) is never placed. A node that already has a parent is AlreadyPlaced.
///
/// Concurrency (Plan P): every placement runs in a transaction. A leaf takes <c>dual-tree</c> shared
/// BEFORE resolving the slot (review I1), then the slot, re-checks inside and retries if another writer got
/// there first; a provisional-root move takes <c>dual-tree</c> exclusive from the start (never an upgrade).
/// UX_DualTeamTree_ParentMemberId_Side, IX_DualTeamTree_MemberId and UX_DualTeamTree_BinaryPath are the hard
/// backstops (their violation also retries). Leg points and counts are maintained incrementally.
/// </summary>
public interface IPlacementService
{
    /// <summary>Place one member. Transactional, idempotent, concurrency-safe. Maintains leg points.</summary>
    Task<Result<PlacementOutcome>> PlaceAsync(string memberId, string sponsorMemberId, CancellationToken ct = default);

    /// <summary>Same as <see cref="PlaceAsync(string,string,CancellationToken)"/>, signed by <paramref name="audit"/>.</summary>
    Task<Result<PlacementOutcome>> PlaceAsync(string memberId, string sponsorMemberId, PlacementAudit audit, CancellationToken ct = default);

    /// <summary>
    /// Plan P: places one by one through <see cref="PlaceAsync(string,string,CancellationToken)"/>, by enroll date
    /// and with a pending sponsor before the members it sponsors (review I2), so a root is only created when the
    /// sponsor is really not in the tree nor pending in the batch. Each member commits on its own.
    /// </summary>
    Task<Result<BulkPlacementOutcome>> PlaceBulkAsync(
        IReadOnlyList<(string MemberId, string SponsorMemberId)> pairs, CancellationToken ct = default);
}

public class PlacementService : IPlacementService
{
    /// <summary>
    /// Intentos cuando otro escritor ocupa el hueco calculado (o gana la carrera del indice). Los derrames de
    /// muchos patrocinadores caen en la misma cola de cadena: con N escritores simultaneos sobre ella, en cada
    /// ronda gana uno, asi que un perdedor puede necesitar hasta N-1 rondas.
    /// </summary>
    private const int MaxIntentos = 50;

    /// <summary>Los indices unicos cuya violacion significa «otro escritor llego antes»: se reintenta.</summary>
    private static readonly string[] IndicesEsperados =
    {
        DualTeamSlotTaken.IndexName,                       // el hueco
        "IX_DualTeamTree_MemberId",                        // el miembro (o la raiz del patrocinador)
        "UX_DualTeamTree_BinaryPath",                      // la clave
        "IX_DualTeamLegFrontiers_SponsorMemberId",         // la fila de cola
    };

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<PlacementService> _log;

    public PlacementService(AppDbContext db, IDateTimeProvider clock)
        : this(db, clock, NullLogger<PlacementService>.Instance) { }

    public PlacementService(AppDbContext db, IDateTimeProvider clock, ILogger<PlacementService> log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

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
        if (memberId == sponsorMemberId)
            return Result<PlacementOutcome>.Failure("INVALID_SPONSOR", "A member cannot sponsor itself.");

        for (var intento = 1; intento <= MaxIntentos; intento++)
        {
            var existing = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
            if (existing is not null && existing.ParentMemberId is not null)
                return Result<PlacementOutcome>.Success(new PlacementOutcome(
                    memberId, existing.ParentMemberId, existing.Side, existing.Depth ?? 0, true));

            if (existing is not null)
            {
                if (!await IsProvisionalRootAsync(memberId, ct))
                    return Result<PlacementOutcome>.Failure("ROOT_CANNOT_BE_PLACED",
                        $"{memberId} is a system root (no sponsor): it is never placed.");
                var movido = await MoveProvisionalRootAsync(memberId, sponsorMemberId, audit, ct);
                if (movido is not null) return movido;
                continue;   // cambio el estado mientras tanto: se vuelve a mirar
            }

            var colocado = await PlaceLeafAsync(memberId, sponsorMemberId, audit, ct);
            if (colocado is not null) return colocado;
        }
        return Result<PlacementOutcome>.Failure("PLACEMENT_CONFLICT", "The slot kept changing; retry later.");
    }

    /// <summary>Un intento de colocar una hoja nueva. Null = otro escritor llego antes, reintentar.</summary>
    private async Task<Result<PlacementOutcome>?> PlaceLeafAsync(
        string memberId, string sponsorMemberId, PlacementAudit audit, CancellationToken ct)
    {
        // Revision I1: si el admin lo retiro, su equipo le espera flotando y vuelve con el; recolgarlo mueve subarboles,
        // asi que entonces el arbol se toma en exclusivo desde el principio (nunca se sube de compartido a exclusivo).
        var flotantes = await DualTeamNodes.FloatingChildrenAsync(_db, memberId, ct);
        await using var scope = await PlacementScope.BeginAsync(_db, _log, ct);
        try
        {
            // Revision I1: el arbol en compartido ANTES de resolver: ningun movimiento puede cambiarlo entre la
            // resolucion y el candado del hueco.
            if (flotantes.Count > 0) await DualTeamGate.EnterTreeExclusiveAsync(_db, ct);
            else await DualTeamGate.EnterTreeSharedAsync(_db, ct);
            var target = await ResolveTargetAsync(sponsorMemberId, audit, scope, ct);

            // Plan P: el candado es del HUECO destino. Dentro se vuelve a comprobar todo.
            if (flotantes.Count == 0) await DualTeamGate.EnterSlotAsync(_db, target.Parent.MemberId, target.Side, ct);
            if (await _db.DualTeamTree.AnyAsync(d => d.MemberId == memberId, ct)
                || await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == target.Parent.MemberId && d.Side == target.Side, ct)
                || (await DualTeamNodes.FloatingChildrenAsync(_db, memberId, ct)).Count != flotantes.Count)
                return await RetryAsync(scope, ct);

            var node = await DualTeamNodes.ChildAsync(_db, target.Parent, memberId, target.Side, _clock.Now, audit.CreatedBy, ct);
            scope.Track(_db.DualTeamTree.Add(node).Entity);
            scope.Track(_db.PlacementLogs.Add(NewLog(memberId, target.Parent.MemberId, target.Side, audit, audit.Reason)).Entity);
            await UpsertFrontierAsync(sponsorMemberId, target, memberId, node.Depth ?? 0, scope, ct);
            await _db.SaveChangesAsync(ct);

            // Plan Q (P-R9): solo se encola; ApplyDualLegDeltasJob sube los puntos y contadores por la linea.
            await LegPointMaintenance.EnqueueLeafAsync(_db, memberId, _clock.Now, audit.CreatedBy, ct);
            await DualTeamNodes.ReattachFloatingChildrenAsync(_db, memberId, flotantes, _clock.Now, audit.CreatedBy, ct);

            await scope.CompleteAsync(ct);
            return Result<PlacementOutcome>.Success(new PlacementOutcome(
                memberId, target.Parent.MemberId, target.Side, node.Depth ?? 0, false));
        }
        catch (Exception ex) when (EsReintentable(ex))
        {
            _log.LogWarning(ex, "Placement of {MemberId} lost a race ({Reason}); retrying.", memberId, ex.GetBaseException().Message);
            return await RetryAsync(scope, ct);
        }
        catch (DualTeamBusyException ex)
        {
            return await FailAsync(scope, "PLACEMENT_BUSY", ex.Message, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Placement of {MemberId} under sponsor {SponsorId} failed.", memberId, sponsorMemberId);
            return await FailAsync(scope, "PLACEMENT_FAILED", ex.Message, ct);
        }
    }

    /// <summary>
    /// Coloca una raiz provisional con todo su subarbol (decision de Nick): la forma interna no cambia, el destino
    /// lo dan las reglas a/b/c bajo su patrocinador y todo su volumen acumulado sube a la linea nueva. Toma el
    /// arbol en exclusivo desde el principio. Null = el estado cambio, reintentar.
    /// </summary>
    private async Task<Result<PlacementOutcome>?> MoveProvisionalRootAsync(
        string memberId, string sponsorMemberId, PlacementAudit audit, CancellationToken ct)
    {
        await using var scope = await PlacementScope.BeginAsync(_db, _log, ct);
        try
        {
            await DualTeamGate.EnterTreeExclusiveAsync(_db, ct);
            var root = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
            if (root is null || root.ParentMemberId is not null) return await RetryAsync(scope, ct);

            var sponsor = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == sponsorMemberId, ct);
            if (sponsor is null)
                return await FailAsync(scope, "SPONSOR_NOT_PLACED",
                    $"{memberId} is a provisional root; it is placed once its sponsor {sponsorMemberId} is in the tree.", ct);
            if (root.BinaryPath is not null && sponsor.BinaryPath is not null && DualBinaryPath.Contains(root.BinaryPath, sponsor.BinaryPath))
                return await FailAsync(scope, "CIRCULAR_REFERENCE",
                    $"The sponsor {sponsorMemberId} is inside the team of {memberId}.", ct);

            var target = await ResolveTargetAsync(sponsorMemberId, audit, scope, ct);
            await DualTeamNodes.MoveSubtreeAsync(_db, memberId,
                new DualTeamDestination.UnderParent(target.Parent, target.Side), adjustLegs: true, _clock.Now, audit.CreatedBy, ct);

            var now = _clock.Now;
            scope.Track(_db.PlacementLogs.Add(NewLog(memberId, target.Parent.MemberId, target.Side, audit,
                audit.Reason + " (provisional root moved with its team)")).Entity);
            // Su equipo suma de golpe en la linea nueva: se evalua el rango del padre nuevo (una fila, no una por
            // ascendiente: revision I4).
            scope.Track(_db.RankEvaluationQueue.Add(new RankEvaluationQueue
            {
                TriggerMemberId = memberId, EvaluateMemberId = target.Parent.MemberId,
                TriggerEvent = RankEvaluationTrigger.Placement, TriggerDate = now,
                CreatedBy = audit.CreatedBy, CreationDate = now
            }).Entity);
            await _db.SaveChangesAsync(ct);

            var moved = await _db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == memberId, ct);
            await scope.CompleteAsync(ct);
            return Result<PlacementOutcome>.Success(new PlacementOutcome(
                memberId, target.Parent.MemberId, target.Side, moved.Depth ?? 0, false));
        }
        catch (Exception ex) when (EsReintentable(ex))
        {
            _log.LogWarning(ex, "Moving provisional root {MemberId} lost a race; retrying.", memberId);
            return await RetryAsync(scope, ct);
        }
        catch (DualTeamBusyException ex)
        {
            return await FailAsync(scope, "PLACEMENT_BUSY", ex.Message, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Moving provisional root {MemberId} under {SponsorId} failed.", memberId, sponsorMemberId);
            return await FailAsync(scope, "PLACEMENT_FAILED", ex.Message, ct);
        }
    }

    /// <summary>Un nodo sin padre es raiz provisional si su miembro tiene patrocinador; si no, es raiz del sistema.</summary>
    private Task<bool> IsProvisionalRootAsync(string memberId, CancellationToken ct) =>
        _db.MemberProfiles.AsNoTracking().AnyAsync(m => m.MemberId == memberId && m.SponsorMemberId != null, ct);

    private bool EsReintentable(Exception ex) => ex switch
    {
        DbUpdateException { InnerException: SqlException s } => EsViolacionEsperada(s),
        SqlException s when s.Number == 1205 => true,   // victima de un interbloqueo
        SqlException s => EsViolacionEsperada(s),
        _ => false,
    };

    private static bool EsViolacionEsperada(SqlException s) =>
        s.Number is 2601 or 2627 && IndicesEsperados.Any(i => s.Message.Contains(i, StringComparison.Ordinal));

    private static async Task<Result<PlacementOutcome>?> RetryAsync(PlacementScope scope, CancellationToken ct)
    {
        if (!await scope.UndoAsync(ct))
            return Result<PlacementOutcome>.Failure("PLACEMENT_FAILED",
                "Could not roll back to the savepoint of the caller's transaction; the transaction must be abandoned.");
        await Task.Delay(Random.Shared.Next(2, 20), ct);   // que los perdedores no vuelvan todos a la vez
        return null;
    }

    private static async Task<Result<PlacementOutcome>> FailAsync(PlacementScope scope, string code, string message, CancellationToken ct)
    {
        await scope.UndoAsync(ct);
        return Result<PlacementOutcome>.Failure(code, message);
    }

    private sealed record Target(DualTeamEntity Parent, TreeSide Side, bool IsSpill, TreeSide PreferredSide);

    /// <summary>Resolve the placement slot for a sponsor by rules a/b/c (queries the live tree).</summary>
    private async Task<Target> ResolveTargetAsync(
        string sponsorMemberId, PlacementAudit audit, PlacementScope scope, CancellationToken ct)
    {
        var sponsorNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == sponsorMemberId, ct);

        if (sponsorNode is null)
        {
            // El patrocinador aun no esta en el binario: se crea como raiz provisional y se coloca debajo (Left).
            // Dos colocaciones que crean la misma raiz chocan en IX_DualTeamTree_MemberId y la perdedora reintenta.
            var root = await DualTeamNodes.RootAsync(_db, sponsorMemberId, _clock.Now, audit.CreatedBy, ct);
            scope.Track(_db.DualTeamTree.Add(root).Entity);
            await _db.SaveChangesAsync(ct);
            return new Target(root, TreeSide.Left, false, TreeSide.Left);
        }

        var preferred = DualTeamPlacementRules.PreferredSide(sponsorNode.ParentMemberId is null, sponsorNode.Side);

        var children = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == sponsorMemberId)
            .Select(d => d.Side)
            .ToListAsync(ct);
        var hasLeft = children.Contains(TreeSide.Left);
        var hasRight = children.Contains(TreeSide.Right);

        DualTeamEntity? deep = null;
        bool deepHasLeft = false;
        if (hasLeft && hasRight)
        {
            // Rule c — spill to the deepest open node on the preferred side. Plan P: siempre se resuelve
            // la cola y su lado libre sale de la BD.
            deep = await ResolveDeepestAsync(sponsorNode, preferred, ct);
            var deepId = deep.MemberId;
            deepHasLeft = await _db.DualTeamTree.AsNoTracking()
                .AnyAsync(d => d.ParentMemberId == deepId && d.Side == TreeSide.Left, ct);
        }

        var decision = DualTeamPlacementRules.Decide(sponsorMemberId, hasLeft, hasRight, deep?.MemberId, deepHasLeft);
        return new Target(decision.IsSpill ? deep! : sponsorNode, decision.Side, decision.IsSpill, preferred);
    }

    /// <summary>
    /// Deepest open node on the sponsor's preferred side. Reads the cached frontier first (O(1)) and accepts it
    /// only if it is still in that leg (by its key) with an open slot; otherwise one query over the leg's key
    /// range (Plan P). The deepest node of a subtree is a leaf; ties by (CreationDate, Id), deterministic.
    /// </summary>
    private async Task<DualTeamEntity> ResolveDeepestAsync(DualTeamEntity sponsor, TreeSide preferred, CancellationToken ct)
    {
        var frontier = await _db.DualTeamLegFrontiers.AsNoTracking()
            .FirstOrDefaultAsync(f => f.SponsorMemberId == sponsor.MemberId, ct);
        if (frontier?.DeepestMemberId is not null)
        {
            var cached = await _db.DualTeamTree.AsNoTracking()
                .FirstOrDefaultAsync(d => d.MemberId == frontier.DeepestMemberId, ct);
            if (cached is not null
                && EnLaPierna(sponsor, preferred, cached)
                && await _db.DualTeamTree.CountAsync(d => d.ParentMemberId == cached.MemberId, ct) < 2)
                return cached;
        }

        var deepest = await DualSubtree.Leg(_db, sponsor, preferred).AsNoTracking()
            .Where(d => _db.DualTeamTree.Count(k => k.ParentMemberId == d.MemberId) < 2)
            .OrderByDescending(d => d.Depth).ThenBy(d => d.CreationDate).ThenBy(d => d.Id)
            .FirstOrDefaultAsync(ct);
        // Una pierna con hijo siempre tiene una hoja: si no aparece nada, el arbol esta corrupto (por ejemplo,
        // un ciclo) y se falla en vez de inventar un destino.
        return deepest ?? throw new InvalidOperationException(
            $"No open node found on the {preferred} leg of {sponsor.MemberId}.");
    }

    private static bool EnLaPierna(DualTeamEntity sponsor, TreeSide side, DualTeamEntity candidate)
    {
        if (sponsor.BinaryPath is null || candidate.BinaryPath is null) return false;
        var (lo, hi) = DualBinaryPath.LegBounds(sponsor.BinaryPath, side);
        return DualBinaryPath.SqlCompare(candidate.BinaryPath, lo) > 0 && DualBinaryPath.SqlCompare(candidate.BinaryPath, hi) < 0;
    }

    private async Task UpsertFrontierAsync(string sponsorMemberId, Target target, string newMemberId, int newDepth,
        PlacementScope scope, CancellationToken ct)
    {
        // The frontier tracks the deepest node on the preferred side. It advances when this placement extends
        // the preferred-side chain: a spill, or the sponsor's preferred-side direct child (rule a/b). Revision:
        // la fila de un patrocinador la escriben colocaciones a huecos distintos; su candado la serializa.
        var decision = new DualTeamPlacementRules.SlotDecision(target.Parent.MemberId, target.Side, target.IsSpill);
        if (!DualTeamPlacementRules.ExtendsPreferredSide(decision, sponsorMemberId, target.PreferredSide)) return;

        await DualTeamGate.EnterFrontierAsync(_db, sponsorMemberId, ct);
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

    private PlacementLog NewLog(string memberId, string parentId, TreeSide side, PlacementAudit audit, string reason)
    {
        var now = _clock.Now;
        return new PlacementLog
        {
            MemberId = memberId,
            PlacedUnderMemberId = parentId,
            Side = side,
            Action = audit.Action,
            Reason = reason,
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
    /// Plan P: cada miembro pasa por el mismo nucleo que PlaceAsync. Orden (revision I2): por fecha de alta y,
    /// si el patrocinador de un miembro tambien esta en la tanda, primero el patrocinador. Asi solo se crea una
    /// raiz provisional cuando el patrocinador de verdad no esta en el binario ni pendiente en la tanda.
    /// </summary>
    public async Task<Result<BulkPlacementOutcome>> PlaceBulkAsync(
        IReadOnlyList<(string MemberId, string SponsorMemberId)> pairs, CancellationToken ct = default)
    {
        if (pairs is null || pairs.Count == 0)
            return Result<BulkPlacementOutcome>.Success(new BulkPlacementOutcome(0, 0, 0));

        var unicos = pairs.DistinctBy(p => p.MemberId).ToList();
        var ids = unicos.Select(p => p.MemberId).ToList();
        var altas = new Dictionary<string, DateTime>();
        foreach (var trozo in ids.Chunk(1000))
            foreach (var m in await _db.MemberProfiles.AsNoTracking().Where(m => trozo.Contains(m.MemberId))
                         .Select(m => new { m.MemberId, m.EnrollDate }).ToListAsync(ct))
                altas[m.MemberId] = m.EnrollDate;

        var porMiembro = unicos.ToDictionary(p => p.MemberId);
        var ordenados = unicos
            .OrderBy(p => altas.TryGetValue(p.MemberId, out var a) ? a : DateTime.MaxValue)
            .ThenBy(p => p.MemberId, StringComparer.Ordinal).ToList();

        int placed = 0, skipped = 0, failed = 0;
        var hechos = new HashSet<string>();
        foreach (var inicial in ordenados)
        {
            // Pila: el patrocinador pendiente de la tanda va antes (sin recursion: las cadenas pueden ser largas).
            var pila = new Stack<(string MemberId, string SponsorMemberId)>();
            pila.Push(inicial);
            while (pila.Count > 0)
            {
                var actual = pila.Peek();
                if (hechos.Contains(actual.MemberId)) { pila.Pop(); continue; }
                if (porMiembro.TryGetValue(actual.SponsorMemberId, out var patrocinador)
                    && !hechos.Contains(patrocinador.MemberId) && !pila.Contains(patrocinador))
                {
                    pila.Push(patrocinador);
                    continue;
                }
                pila.Pop();
                hechos.Add(actual.MemberId);
                var r = await PlaceAsync(actual.MemberId, actual.SponsorMemberId, ct);
                if (!r.IsSuccess) failed++;
                else if (r.Value!.AlreadyPlaced) skipped++;
                else placed++;
            }
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
        private readonly ILogger _log;
        private readonly IDbContextTransaction? _own;
        private readonly bool _savepoint;
        private readonly List<object> _tracked = new();
        private bool _done;

        private PlacementScope(AppDbContext db, ILogger log, IDbContextTransaction? own, bool savepoint)
        {
            _db = db; _log = log; _own = own; _savepoint = savepoint;
        }

        public bool OwnsTransaction => _own is not null;

        public static async Task<PlacementScope> BeginAsync(AppDbContext db, ILogger log, CancellationToken ct)
        {
            if (db.Database.IsRelational() && db.Database.CreateExecutionStrategy().RetriesOnFailure)
                throw new InvalidOperationException(
                    "PlacementService needs the placement to run inside the execution strategy (EnableRetryOnFailure is on).");

            var ambient = db.Database.CurrentTransaction;
            if (ambient is null)
                return new PlacementScope(db, log, await db.Database.BeginTransactionAsync(ct), false);

            // Dentro de la transaccion de quien llama: sus cambios pendientes se guardarian con los nuestros y un
            // reintento los perderia o duplicaria. Se exige el contexto limpio.
            if (db.ChangeTracker.HasChanges())
                throw new InvalidOperationException(
                    "PlacementService inside the caller's transaction needs no pending changes in the context.");
            var savepoint = db.Database.IsRelational() && ambient.SupportsSavepoints;
            if (savepoint) await ambient.CreateSavepointAsync(Savepoint, ct);
            return new PlacementScope(db, log, null, savepoint);
        }

        public void Track(object entity) => _tracked.Add(entity);

        public async Task CompleteAsync(CancellationToken ct)
        {
            if (_own is not null) await _own.CommitAsync(ct);
            _done = true;
            Detach();
        }

        /// <summary>Deshace. False si no se pudo volver al punto de guardado de quien llama.</summary>
        public async Task<bool> UndoAsync(CancellationToken ct)
        {
            if (_done) return true;
            _done = true;
            var ok = true;
            try
            {
                if (_own is not null) await _own.RollbackAsync(CancellationToken.None);
                else if (_savepoint) await _db.Database.CurrentTransaction!.RollbackToSavepointAsync(Savepoint, CancellationToken.None);
            }
            catch (Exception ex) when (_own is not null)
            {
                // Victima de un interbloqueo o conexion caida: SQL Server ya descarto la transaccion.
                _log.LogDebug(ex, "Rollback of a placement transaction already discarded by SQL Server.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Could not roll back to the placement savepoint inside the caller's transaction.");
                ok = false;
            }
            Detach();
            return ok;
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
