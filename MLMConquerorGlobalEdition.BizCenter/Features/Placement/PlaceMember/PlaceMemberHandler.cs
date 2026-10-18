using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;

/// <summary>
/// Colocacion manual desde el BizCenter (el patrocinador elige padre y lado). Plan P:
/// <list type="bullet">
/// <item>Un miembro sin nodo entra como hoja: arbol en compartido, candado del hueco, comprobacion dentro.</item>
/// <item>Una raiz provisional (tiene nodo y no tiene padre; decision de Nick) se coloca con todo su equipo:
/// el subarbol se mueve sin cambiar de forma y su volumen acumulado sube a la linea nueva.</item>
/// <item>Un nodo ya colocado se mueve con sus reglas de siempre (sin downline propio en su subarbol).</item>
/// <item>Los movimientos toman el arbol en exclusivo desde el principio y van por DualTeamNodes (clave, ruta,
/// piernas incrementales y colas). Ningun filtro por HierarchyPath (C1).</item>
/// </list>
/// </summary>
public class PlaceMemberHandler : IRequestHandler<PlaceMemberCommand, Result<PlaceMemberResult>>
{
    private const int PlacementWindowDays       = 30;
    private const int MaxPlacementOpportunities = 2;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public PlaceMemberHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
    }

    private enum Tipo { Hoja, RaizProvisional, Movimiento }

    public async Task<Result<PlaceMemberResult>> Handle(
        PlaceMemberCommand command, CancellationToken ct)
    {
        // Hora del servidor (la regla del proyecto): la ventana se compara con EnrollDate, guardada igual.
        var now      = _clock.Now;
        var isAdmin  = _currentUser.IsAdmin;
        var side     = Enum.Parse<TreeSide>(command.Side);

        var member = await _db.MemberProfiles.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == command.MemberToPlaceId && !m.IsDeleted, ct);

        if (member is null)
            return Result<PlaceMemberResult>.Failure("MEMBER_NOT_FOUND",
                "El miembro que intenta colocar no existe.");

        if (member.Status == MemberAccountStatus.Terminated)
            return Result<PlaceMemberResult>.Failure("MEMBER_CANCELLED",
                "No se puede colocar a un ambassador con estado Cancelado.");

        var placementLog = await _db.PlacementLogs.AsNoTracking()
            .Where(p => p.MemberId == command.MemberToPlaceId)
            .OrderByDescending(p => p.CreationDate)
            .FirstOrDefaultAsync(ct);

        var opportunitiesUsed = placementLog?.UnplacementCount ?? 0;

        if (!isAdmin)
        {
            if (now > member.EnrollDate.AddDays(PlacementWindowDays))
                throw new PlacementWindowExpiredException();

            if (opportunitiesUsed >= MaxPlacementOpportunities)
                throw new UnplacementLimitExceededException();
        }

        if (command.TargetParentMemberId == command.MemberToPlaceId)
            return Result<PlaceMemberResult>.Failure("AUTO_SUPERIORITY",
                "Un ambassador no puede ser su propio superior directo.");

        var targetParent = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == command.TargetParentMemberId, ct);

        if (targetParent is null)
            return Result<PlaceMemberResult>.Failure("TARGET_NOT_FOUND",
                "El nodo destino no existe en el Dual Team.");

        if (await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == command.TargetParentMemberId && d.Side == side, ct))
            return SlotOccupied(command);

        var existingNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == command.MemberToPlaceId, ct);
        var tipo = existingNode is null ? Tipo.Hoja
            : existingNode.ParentMemberId is null ? Tipo.RaizProvisional
            : Tipo.Movimiento;

        if (tipo == Tipo.RaizProvisional && member.SponsorMemberId is null)
            return Result<PlaceMemberResult>.Failure("ROOT_CANNOT_BE_PLACED",
                "Una raiz del sistema (sin patrocinador) no se coloca.");

        if (existingNode is not null && IsInside(existingNode, targetParent))
            return Circular();

        if (tipo == Tipo.Movimiento && await HasOwnEnrolledDownlineAsync(existingNode!, ct))
            return Result<PlaceMemberResult>.Failure("HAS_OWN_DOWNLINE",
                "No se puede mover a este ambassador porque ya tiene downline propio " +
                "en la estructura Dual Team.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Plan P: el candado antes de comprobar nada (I1). Una hoja: arbol en compartido y su hueco. Un
            // movimiento: arbol en exclusivo desde el principio (nunca se sube de compartido a exclusivo).
            if (tipo == Tipo.Hoja)
            {
                await DualTeamGate.EnterTreeSharedAsync(_db, ct);
                await DualTeamGate.EnterSlotAsync(_db, command.TargetParentMemberId, side, ct);
            }
            else
            {
                await DualTeamGate.EnterTreeExclusiveAsync(_db, ct);
            }

            // Con el candado: el destino, el hueco y el estado del miembro se vuelven a leer.
            targetParent = await _db.DualTeamTree.AsNoTracking()
                .FirstOrDefaultAsync(d => d.MemberId == command.TargetParentMemberId, ct);
            if (targetParent is null)
            {
                await tx.RollbackAsync(ct);
                return Result<PlaceMemberResult>.Failure("TARGET_NOT_FOUND", "El nodo destino no existe en el Dual Team.");
            }
            if (await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == command.TargetParentMemberId && d.Side == side, ct))
            {
                await tx.RollbackAsync(ct);
                return SlotOccupied(command);
            }
            var nodoAhora = await _db.DualTeamTree.AsNoTracking()
                .FirstOrDefaultAsync(d => d.MemberId == command.MemberToPlaceId, ct);
            var tipoAhora = nodoAhora is null ? Tipo.Hoja : nodoAhora.ParentMemberId is null ? Tipo.RaizProvisional : Tipo.Movimiento;
            if (tipoAhora != tipo)
            {
                await tx.RollbackAsync(ct);
                return Result<PlaceMemberResult>.Failure("PLACEMENT_CONFLICT",
                    "La posicion del miembro cambio mientras se colocaba. Vuelve a intentarlo.");
            }
            if (nodoAhora is not null && IsInside(nodoAhora, targetParent))
            {
                await tx.RollbackAsync(ct);
                return Circular();
            }
            // Revision (menor 5): con el arbol en exclusivo se repite: entre la comprobacion de fuera y el candado pudo
            // colocarse debajo alguno de sus patrocinados.
            if (tipo == Tipo.Movimiento && await HasOwnEnrolledDownlineAsync(nodoAhora!, ct))
            {
                await tx.RollbackAsync(ct);
                return Result<PlaceMemberResult>.Failure("HAS_OWN_DOWNLINE",
                    "No se puede mover a este ambassador porque ya tiene downline propio en la estructura Dual Team.");
            }

            if (tipo == Tipo.Hoja)
            {
                var nuevo = await DualTeamNodes.ChildAsync(_db, targetParent, command.MemberToPlaceId, side, now, _currentUser.UserId, ct);
                _db.DualTeamTree.Add(nuevo);
                await _db.SaveChangesAsync(ct);
                await LegPointMaintenance.ApplyIncrementalAsync(_db, new[] { command.MemberToPlaceId }, ct);
            }
            else
            {
                await DualTeamNodes.MoveSubtreeAsync(_db, command.MemberToPlaceId,
                    new DualTeamDestination.UnderParent(targetParent, side), adjustLegs: true, now, _currentUser.UserId, ct);
            }

            var isFirstPlacement  = placementLog is null;
            var newOpportunities  = opportunitiesUsed + 1;

            _db.PlacementLogs.Add(new PlacementLog
            {
                MemberId              = command.MemberToPlaceId,
                PlacedUnderMemberId   = command.TargetParentMemberId,
                Side                  = side,
                Action                = "Placed",
                Reason                = (isAdmin ? "Admin placement" : "Ambassador placement")
                                        + (tipo == Tipo.RaizProvisional ? " (provisional root moved with its team)" : string.Empty),
                UnplacementCount      = newOpportunities,
                FirstPlacementDate    = isFirstPlacement ? now : placementLog!.FirstPlacementDate,
                CreationDate          = now,
                CreatedBy             = _currentUser.UserId
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var memberName = $"{member.FirstName} {member.LastName}";
            return Result<PlaceMemberResult>.Success(new PlaceMemberResult(
                command.MemberToPlaceId,
                memberName,
                command.TargetParentMemberId,
                command.Side,
                Math.Max(0, MaxPlacementOpportunities - newOpportunities)
            ));
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
            return Result<PlaceMemberResult>.Failure("PLACEMENT_BUSY",
                "Hay otra colocacion en curso en esta parte del arbol. Intentalo de nuevo en unos segundos.");
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static Result<PlaceMemberResult> SlotOccupied(PlaceMemberCommand command) =>
        Result<PlaceMemberResult>.Failure("SLOT_OCCUPIED",
            $"El nodo {command.TargetParentMemberId} ya tiene ocupada la pierna {command.Side}. " +
            "La estructura es estrictamente binaria.");

    private static Result<PlaceMemberResult> Circular() =>
        Result<PlaceMemberResult>.Failure("CIRCULAR_REFERENCE",
            "No se puede realizar este placement: generaría una referencia circular en el árbol.");

    /// <summary>¿Esta <paramref name="target"/> dentro del subarbol de <paramref name="node"/> (el incluido)? Por la clave.</summary>
    private bool IsInside(DualTeamEntity node, DualTeamEntity target)
    {
        if (node.MemberId == target.MemberId) return true;
        if (node.BinaryPath is not null && target.BinaryPath is not null)
            return DualBinaryPath.Contains(node.BinaryPath, target.BinaryPath);
        // Sin claves (InMemory): subir por ParentMemberId desde el destino.
        var vistos = new HashSet<string>();
        for (var actual = target; actual is not null && vistos.Add(actual.MemberId);)
        {
            if (actual.MemberId == node.MemberId) return true;
            var padre = actual.ParentMemberId;
            actual = padre is null ? null : _db.DualTeamTree.AsNoTracking().FirstOrDefault(d => d.MemberId == padre);
        }
        return false;
    }

    /// <summary>
    /// ¿Tiene en su subarbol binario a alguno de sus propios patrocinados? Plan P: el subarbol es el rango de su
    /// clave (DualSubtree.Under) unido a sus patrocinados, en una consulta; antes, prefijo de la ruta.
    /// </summary>
    private Task<bool> HasOwnEnrolledDownlineAsync(DualTeamEntity node, CancellationToken ct) =>
        (from d in DualSubtree.Under(_db, node).AsNoTracking()
         join m in _db.MemberProfiles.AsNoTracking() on d.MemberId equals m.MemberId
         where m.SponsorMemberId == node.MemberId && !m.IsDeleted
         select d.MemberId).AnyAsync(ct);
}
