using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminPlaceMember;

/// <summary>
/// Admin placement bypasses time and opportunity restrictions but still enforces
/// structural rules: no circular reference, no third leg, no auto-superiority.
/// Plan P:
/// <list type="bullet">
/// <item>Un miembro sin nodo entra como hoja (arbol en compartido y su hueco). Si tiene hijos esperando de una
/// retirada del admin (flotantes), vuelven con el: entonces es un movimiento.</item>
/// <item>Una raiz provisional se coloca con todo su equipo; una raiz del sistema no se coloca.</item>
/// <item>Un nodo colocado se mueve con su subarbol.</item>
/// <item>Los movimientos toman el arbol en exclusivo desde el principio y van por DualTeamNodes (clave, ruta,
/// piernas incrementales y colas). Ningun filtro por HierarchyPath (C1).</item>
/// </list>
/// </summary>
public class AdminPlaceMemberHandler : IRequestHandler<AdminPlaceMemberCommand, Result<string>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public AdminPlaceMemberHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public async Task<Result<string>> Handle(AdminPlaceMemberCommand command, CancellationToken ct)
    {
        var now  = _clock.Now;
        var side = Enum.Parse<TreeSide>(command.Side);

        var member = await _db.MemberProfiles.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == command.MemberToPlaceId && !m.IsDeleted, ct);

        if (member is null)
            return Result<string>.Failure("MEMBER_NOT_FOUND", "El miembro que intenta colocar no existe.");

        if (command.TargetParentMemberId == command.MemberToPlaceId)
            return Result<string>.Failure("AUTO_SUPERIORITY",
                "Un ambassador no puede ser su propio superior directo.");

        var targetParent = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == command.TargetParentMemberId, ct);

        if (targetParent is not null
            && await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == command.TargetParentMemberId && d.Side == side, ct))
            return SlotOccupied(command);

        var existingNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == command.MemberToPlaceId, ct);
        if (existingNode is not null && existingNode.ParentMemberId is null && member.SponsorMemberId is null)
            return Result<string>.Failure("ROOT_CANNOT_BE_PLACED", "Una raiz del sistema (sin patrocinador) no se coloca.");
        if (existingNode is not null && targetParent is not null && IsInside(existingNode, targetParent))
            return Circular();

        // Un movimiento: el miembro tiene nodo, o no lo tiene pero le esperan hijos de una retirada del admin.
        var floating = existingNode is null
            ? await DualTeamNodes.FloatingChildrenAsync(_db, command.MemberToPlaceId, ct)
            : new List<DualTeamEntity>();
        var esMovimiento = existingNode is not null || floating.Count > 0;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Plan P (I1): el candado antes de comprobar nada; un movimiento, el arbol en exclusivo desde el
            // principio (sin pasar de compartido a exclusivo).
            if (esMovimiento)
                await DualTeamGate.EnterTreeExclusiveAsync(_db, ct);
            else
            {
                await DualTeamGate.EnterTreeSharedAsync(_db, ct);
                await DualTeamGate.EnterSlotAsync(_db, command.TargetParentMemberId, side, ct);
            }

            // Con el candado: el hueco, el destino y el estado del miembro se vuelven a leer.
            if (await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == command.TargetParentMemberId && d.Side == side, ct))
            {
                await tx.RollbackAsync(ct);
                return SlotOccupied(command);
            }
            var ahora = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == command.MemberToPlaceId, ct);
            if ((ahora is null) != (existingNode is null)
                || (ahora is not null && existingNode is not null && ahora.ParentMemberId != existingNode.ParentMemberId))
            {
                await tx.RollbackAsync(ct);
                return Result<string>.Failure("PLACEMENT_CONFLICT", "La posicion del miembro cambio mientras se colocaba. Vuelve a intentarlo.");
            }

            targetParent = await _db.DualTeamTree.AsNoTracking()
                .FirstOrDefaultAsync(d => d.MemberId == command.TargetParentMemberId, ct);
            // Auto-create the parent as a root node if it doesn't exist in the dual tree yet.
            // This happens on first placement under an ambassador who was never placed themselves.
            if (targetParent is null)
            {
                targetParent = await DualTeamNodes.RootAsync(_db, command.TargetParentMemberId, now, _currentUser.UserId, ct);
                _db.DualTeamTree.Add(targetParent);
                await _db.SaveChangesAsync(ct);
                _db.Entry(targetParent).State = EntityState.Detached;
            }
            if (ahora is not null && IsInside(ahora, targetParent))
            {
                await tx.RollbackAsync(ct);
                return Circular();
            }

            if (ahora is not null)
            {
                // Nodo colocado o raiz provisional: el subarbol se mueve entero.
                await DualTeamNodes.MoveSubtreeAsync(_db, command.MemberToPlaceId,
                    new DualTeamDestination.UnderParent(targetParent, side), adjustLegs: true, now, _currentUser.UserId, ct);
            }
            else
            {
                var nuevo = await DualTeamNodes.ChildAsync(_db, targetParent, command.MemberToPlaceId, side, now, _currentUser.UserId, ct);
                _db.DualTeamTree.Add(nuevo);
                await _db.SaveChangesAsync(ct);
                await LegPointMaintenance.ApplyIncrementalAsync(_db, new[] { command.MemberToPlaceId }, ct);
                _db.Entry(nuevo).State = EntityState.Detached;

                // Member was previously unplaced — their descendants are floating. Re-attach them to the new position.
                var colocado = await _db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == command.MemberToPlaceId, ct);
                foreach (var hijo in await DualTeamNodes.FloatingChildrenAsync(_db, command.MemberToPlaceId, ct))
                    await DualTeamNodes.MoveSubtreeAsync(_db, hijo.MemberId,
                        new DualTeamDestination.UnderParent(colocado, hijo.Side), adjustLegs: true, now, _currentUser.UserId, ct);
            }

            var prevLog = await _db.PlacementLogs.AsNoTracking()
                .Where(p => p.MemberId == command.MemberToPlaceId)
                .OrderByDescending(p => p.CreationDate)
                .FirstOrDefaultAsync(ct);

            _db.PlacementLogs.Add(new PlacementLog
            {
                MemberId            = command.MemberToPlaceId,
                PlacedUnderMemberId = command.TargetParentMemberId,
                Side                = side,
                Action              = "Placed",
                Reason              = "Admin override",
                UnplacementCount    = prevLog?.UnplacementCount ?? 0, // admin does not increment
                FirstPlacementDate  = prevLog?.FirstPlacementDate ?? now,
                CreationDate        = now,
                CreatedBy           = _currentUser.UserId
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result<string>.Success(
                $"Placement de {member.FirstName} {member.LastName} completado exitosamente.");
        }
        catch (DbUpdateException ex) when (DualTeamSlotTaken.Is(ex))
        {
            // La red del indice unico de hueco: otro escritor lo ocupo a la vez.
            await tx.RollbackAsync(ct);
            return SlotOccupied(command);
        }
        catch (DualTeamBusyException)
        {
            await tx.RollbackAsync(ct);
            return Result<string>.Failure("PLACEMENT_BUSY",
                "Hay otra colocacion en curso en esta parte del arbol. Intentalo de nuevo en unos segundos.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static Result<string> SlotOccupied(AdminPlaceMemberCommand command) =>
        Result<string>.Failure("SLOT_OCCUPIED",
            $"El nodo {command.TargetParentMemberId} ya tiene ocupada la pierna {command.Side}.");

    private static Result<string> Circular() =>
        Result<string>.Failure("CIRCULAR_REFERENCE",
            "No se puede realizar este placement: generaría una referencia circular en el árbol.");

    /// <summary>¿Esta <paramref name="target"/> dentro del subarbol de <paramref name="node"/> (el incluido)? Por la clave.</summary>
    private bool IsInside(DualTeamEntity node, DualTeamEntity target)
    {
        if (node.MemberId == target.MemberId) return true;
        if (node.BinaryPath is not null && target.BinaryPath is not null)
            return DualBinaryPath.Contains(node.BinaryPath, target.BinaryPath);
        var vistos = new HashSet<string>();
        for (var actual = target; actual is not null && vistos.Add(actual.MemberId);)
        {
            if (actual.MemberId == node.MemberId) return true;
            var padre = actual.ParentMemberId;
            actual = padre is null ? null : _db.DualTeamTree.AsNoTracking().FirstOrDefault(d => d.MemberId == padre);
        }
        return false;
    }
}
