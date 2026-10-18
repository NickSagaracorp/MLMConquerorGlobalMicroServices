using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Trees;
using MLMConquerorGlobalEdition.SharedKernel;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.PlaceMember;

public class PlaceMemberHandler : IRequestHandler<PlaceMemberCommand, Result<bool>>
{
    /// <summary>
    /// SQL Server's nonclustered index on HierarchyPath caps at 1700 bytes
    /// (nvarchar(850)). Sprint-15 Bug B: a degenerate chain blew past that
    /// (1716 > 1700). We refuse to consider any parent slot whose path is
    /// already long enough that adding "{memberId}/" (≈14 chars × 2 bytes)
    /// would risk crossing the limit. 1500-byte cap is the same guard the
    /// BFS PowerShell backfill script uses.
    /// </summary>
    private const int MaxParentHierarchyPathBytes = 1500;

    /// <summary>Plan P: busquedas de hueco cuando otro escritor ocupa el elegido entre la busqueda y el candado.</summary>
    private const int MaxIntentos = 10;

    private readonly AppDbContext               _db;
    private readonly IDateTimeProvider          _dateTime;
    private readonly IPushNotificationService   _push;
    private readonly IDualTeamPointsRecalculator _legPoints;

    public PlaceMemberHandler(
        AppDbContext               db,
        IDateTimeProvider          dateTime,
        IPushNotificationService   push,
        IDualTeamPointsRecalculator legPoints)
    {
        _db        = db;
        _dateTime  = dateTime;
        _push      = push;
        _legPoints = legPoints;
    }

    public async Task<Result<bool>> Handle(PlaceMemberCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        var member = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.MemberId == command.MemberId, ct);
        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", $"Member '{command.MemberId}' not found.");

        // Validate 30-day placement window
        if ((now - member.EnrollDate).TotalDays > 30)
            throw new PlacementWindowExpiredException();

        var parent = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.MemberId == command.PlaceUnderMemberId, ct);
        if (parent is null)
            return Result<bool>.Failure("PARENT_MEMBER_NOT_FOUND", $"Member '{command.PlaceUnderMemberId}' not found.");

        // A member occupies at most one dual-tree position. Without this guard two
        // concurrent placements (or a placement racing the AutoPlacementJob) would
        // both insert, producing duplicate rows. The DB unique index on MemberId is
        // the hard backstop; this check returns a clean error instead of a 500.
        if (await _db.DualTeamTree.AnyAsync(d => d.MemberId == command.MemberId, ct))
            return Result<bool>.Failure("ALREADY_PLACED", $"Member '{command.MemberId}' is already placed in the dual tree.");

        var side = Enum.Parse<TreeSide>(command.Side);

        // Plan P: buscar el hueco, tomar su candado, volver a comprobarlo e insertar, todo en una transaccion.
        // Antes se comprobaba sobre una instantanea y se guardaba suelto: dos peticiones simultaneas (o una
        // contra AutoPlacementJob) elegian el mismo hueco. Si otro escritor lo ocupa entre la busqueda y el
        // candado, se deshace y se busca el siguiente.
        string? parentMemberId = null, parentPath = null;
        for (var intento = 1; intento <= MaxIntentos && parentMemberId is null; intento++)
        {
            var nuevos = new List<object>();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var slot = await FindFirstEmptySlotAsync(command.PlaceUnderMemberId, side, ct);
                if (slot is null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<bool>.Failure(
                        "NO_AVAILABLE_SLOT",
                        $"No available {command.Side} slot found under '{command.PlaceUnderMemberId}' " +
                        $"(all candidates were either occupied or near the 1500-byte HierarchyPath safety cap).");
                }

                await DualTeamGate.EnterSlotAsync(_db, slot.Value.ParentMemberId, side, ct);
                if (await _db.DualTeamTree.AnyAsync(d => d.MemberId == command.MemberId, ct))
                {
                    await tx.RollbackAsync(ct);
                    return Result<bool>.Failure("ALREADY_PLACED", $"Member '{command.MemberId}' is already placed in the dual tree.");
                }
                if (await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == slot.Value.ParentMemberId && d.Side == side, ct))
                {
                    await tx.RollbackAsync(ct);
                    continue;   // otro escritor lo ocupo: se busca el siguiente hueco
                }

                var (candidateParent, candidatePath) = slot.Value;
                AddPlacementRows(command, side, candidateParent, candidatePath, now, nuevos);
                await _db.SaveChangesAsync(ct);

                // Sprint-15 Bug C: SignupAPI placements used to leave LeftLegPoints /
                // RightLegPoints stale. Recompute the binary leg sums up the new
                // parent's chain using the same shared service BizCenter uses.
                await _legPoints.RecalculateForUplinesAsync(candidateParent, ct);

                await tx.CommitAsync(ct);
                (parentMemberId, parentPath) = (candidateParent, candidatePath);
            }
            catch (DbUpdateException ex) when (DualTeamSlotTaken.Is(ex))
            {
                await tx.RollbackAsync(ct);   // la red del indice unico de hueco: se busca otro
                foreach (var e in nuevos) _db.Entry(e).State = EntityState.Detached;
            }
            catch (DualTeamBusyException ex)
            {
                await tx.RollbackAsync(ct);
                return Result<bool>.Failure("PLACEMENT_BUSY", ex.Message);
            }
        }

        if (parentMemberId is null || parentPath is null)
            return Result<bool>.Failure("PLACEMENT_CONFLICT",
                $"The {command.Side} slots under '{command.PlaceUnderMemberId}' kept changing; retry later.");

        // Notify the placed member
        _ = _push.SendAsync(
            command.MemberId,
            NotificationEvents.PlacementCompleted,
            "Placement Completed",
            $"You have been placed in the {command.Side} position of your team.",
            ct);

        // Notify all uplines in the dual team hierarchy
        var uplineIds = parentPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => id != command.MemberId)
            .ToList();

        foreach (var uplineId in uplineIds)
        {
            _ = _push.SendAsync(
                uplineId,
                NotificationEvents.DownlinePlaced,
                "New Team Placement",
                $"A new member has been placed in your downline ({command.Side} leg).",
                ct);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>Nodo, log y cola de evaluacion de rango de la colocacion. Anota lo anadido en <paramref name="nuevos"/>.</summary>
    private void AddPlacementRows(PlaceMemberCommand command, TreeSide side, string parentMemberId, string parentPath,
        DateTime now, List<object> nuevos)
    {
        // Sprint-15 Bug 10 — Defensive normalize: legacy parent rows historically
        // existed where HierarchyPath was missing a trailing slash, which then
        // glued IDs together in the child (e.g. /AMB-X/AMB-Y AMB-Z/). TrimEnd
        // guarantees exactly one '/' separator between parent path and new ID.
        var newPath = $"{parentPath.TrimEnd('/')}/{command.MemberId}/";

        nuevos.Add(_db.DualTeamTree.Add(new DualTeamEntity
        {
            MemberId       = command.MemberId,
            ParentMemberId = parentMemberId,
            Side           = side,
            HierarchyPath  = newPath,
            CreatedBy      = command.MemberId,
            CreationDate   = now,
            LastUpdateDate = now
        }).Entity);

        nuevos.Add(_db.PlacementLogs.Add(new PlacementLog
        {
            MemberId            = command.MemberId,
            PlacedUnderMemberId = parentMemberId,
            Side                = side,
            Action              = "Placed",
            FirstPlacementDate  = now,
            CreationDate        = now,
            CreatedBy           = command.MemberId
        }).Entity);

        // Queue rank re-evaluation for every dual-team upline of the placement parent
        foreach (var uplineId in parentPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(id => id != command.MemberId))
        {
            nuevos.Add(_db.RankEvaluationQueue.Add(new RankEvaluationQueue
            {
                TriggerMemberId  = command.MemberId,
                EvaluateMemberId = uplineId,
                TriggerEvent     = RankEvaluationTrigger.Placement,
                TriggerDate      = now,
                CreatedBy        = command.MemberId,
                CreationDate     = now
            }).Entity);
        }
    }

    /// <summary>
    /// El hueco menos profundo del lado pedido bajo <paramref name="rootMemberId"/> (regla del producto: el
    /// miembro elige el lado). Plan P: decide por adyacencia (ParentMemberId), nivel a nivel, y se detiene en
    /// el primer nivel con hueco; dentro de un nivel, por (CreationDate, Id). Antes cargaba todo el subarbol por
    /// prefijo de HierarchyPath y decidia sobre esa instantanea. Se mantiene el tope de 1.500 bytes de ruta
    /// (Sprint-15 Bug B) hasta que la clave compacta del Plan P (Tarea 9) traiga el limite real: un nodo por
    /// encima no se ofrece ni se recorre (sus descendientes estan aun mas abajo).
    /// </summary>
    private async Task<(string ParentMemberId, string ParentHierarchyPath)?> FindFirstEmptySlotAsync(
        string rootMemberId, TreeSide requiredSide, CancellationToken ct)
    {
        // Bootstrap — root might not exist in the dual tree at all yet. In that case the slot
        // under it is trivially open and the path is synthesized the same way the seeder does it.
        var rootNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == rootMemberId, ct);
        var nivel = new List<(string Id, string Path)> { (rootMemberId, rootNode?.HierarchyPath ?? $"/{rootMemberId}/") };

        while (nivel.Count > 0)
        {
            nivel = nivel.Where(n => n.Path.Length <= MaxParentHierarchyPathBytes).ToList();
            if (nivel.Count == 0) return null;

            var ids = nivel.Select(n => n.Id).ToList();
            var hijos = await _db.DualTeamTree.AsNoTracking()
                .Where(d => d.ParentMemberId != null && ids.Contains(d.ParentMemberId))
                .Select(d => new { d.MemberId, d.ParentMemberId, d.Side, d.HierarchyPath, d.CreationDate, d.Id })
                .ToListAsync(ct);

            var ocupados = hijos.Where(h => h.Side == requiredSide).Select(h => h.ParentMemberId!).ToHashSet();
            foreach (var n in nivel)
                if (!ocupados.Contains(n.Id))
                    return (n.Id, n.Path);

            nivel = hijos.OrderBy(h => h.CreationDate).ThenBy(h => h.Id, StringComparer.Ordinal)
                .Select(h => (h.MemberId, h.HierarchyPath)).ToList();
        }
        return null;
    }
}
