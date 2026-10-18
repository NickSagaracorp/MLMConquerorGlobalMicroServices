using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Trees;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;

public class PlaceMemberHandler : IRequestHandler<PlaceMemberCommand, Result<PlaceMemberResult>>
{
    private const int PlacementWindowDays       = 30;
    private const int MaxPlacementOpportunities = 2;

    private readonly AppDbContext                _db;
    private readonly ICurrentUserService         _currentUser;
    private readonly IDateTimeProvider           _clock;
    private readonly IDualTeamPointsRecalculator _legPoints;

    public PlaceMemberHandler(
        AppDbContext                db,
        ICurrentUserService         currentUser,
        IDateTimeProvider           clock,
        IDualTeamPointsRecalculator legPoints)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
        _legPoints   = legPoints;
    }

    public async Task<Result<PlaceMemberResult>> Handle(
        PlaceMemberCommand command, CancellationToken ct)
    {
        var now      = _clock.UtcNow;
        var isAdmin  = _currentUser.IsAdmin;
        var side     = Enum.Parse<TreeSide>(command.Side);

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == command.MemberToPlaceId && !m.IsDeleted, ct);

        if (member is null)
            return Result<PlaceMemberResult>.Failure("MEMBER_NOT_FOUND",
                "El miembro que intenta colocar no existe.");

        if (member.Status == MemberAccountStatus.Terminated)
            return Result<PlaceMemberResult>.Failure("MEMBER_CANCELLED",
                "No se puede colocar a un ambassador con estado Cancelado.");

        var placementLog = await _db.PlacementLogs
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

        var targetParent = await _db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == command.TargetParentMemberId, ct);

        if (targetParent is null)
            return Result<PlaceMemberResult>.Failure("TARGET_NOT_FOUND",
                "El nodo destino no existe en el Dual Team.");

        var slotOccupied = await _db.DualTeamTree
            .AnyAsync(d => d.ParentMemberId == command.TargetParentMemberId
                        && d.Side == side, ct);

        if (slotOccupied)
            return Result<PlaceMemberResult>.Failure("SLOT_OCCUPIED",
                $"El nodo {command.TargetParentMemberId} ya tiene ocupada la pierna {command.Side}. " +
                "La estructura es estrictamente binaria.");

        // If the member to place is already in the tree, their hierarchyPath must NOT
        // be a prefix of the target parent's path (i.e. target is not their descendant).
        var existingNode = await _db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == command.MemberToPlaceId, ct);

        if (existingNode != null)
        {
            if (targetParent.HierarchyPath.StartsWith(existingNode.HierarchyPath))
                return Result<PlaceMemberResult>.Failure("CIRCULAR_REFERENCE",
                    "No se puede realizar este placement: generaría una referencia circular en el árbol.");
        }

        if (command.TargetParentMemberId == command.MemberToPlaceId)
            return Result<PlaceMemberResult>.Failure("AUTO_SUPERIORITY",
                "Un ambassador no puede ser su propio superior directo.");

        if (existingNode != null)
        {
            var hasOwnDownline = await HasOwnEnrolledDownlineAsync(
                command.MemberToPlaceId, existingNode.HierarchyPath, ct);

            if (hasOwnDownline)
                return Result<PlaceMemberResult>.Failure("HAS_OWN_DOWNLINE",
                    "No se puede mover a este ambassador porque ya tiene downline propio " +
                    "en la estructura Dual Team.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Plan P: la comprobacion del hueco va DENTRO de la transaccion y detras del candado. Un movimiento
            // reescribe rutas y toma el arbol en exclusivo; un alta toma solo su hueco. Con el candado se
            // vuelven a leer el destino y el nodo (otro movimiento pudo cambiar sus rutas) y el hueco.
            if (existingNode != null)
                await DualTeamGate.EnterTreeExclusiveAsync(_db, ct);
            else
                await DualTeamGate.EnterSlotAsync(_db, command.TargetParentMemberId, side, ct);

            var lockedCheck = await RecheckInsideLockAsync(command, side, existingNode is not null, ct);
            if (lockedCheck is not null)
            {
                await tx.RollbackAsync(ct);
                return lockedCheck;
            }
            // Las entidades ya rastreadas no se refrescan con otra consulta: se recargan.
            await _db.Entry(targetParent).ReloadAsync(ct);
            if (existingNode != null) await _db.Entry(existingNode).ReloadAsync(ct);

            if (existingNode != null)
            {
                _db.DualTeamTree.Remove(existingNode);

                // Detach all descendants — they keep their relative positions
                // (they will be re-parented when we build the new path)
                var descendants = await _db.DualTeamTree
                    .Where(d => d.HierarchyPath.StartsWith(existingNode.HierarchyPath)
                             && d.MemberId != command.MemberToPlaceId)
                    .ToListAsync(ct);

                foreach (var desc in descendants)
                {
                    var relative = desc.HierarchyPath.Substring(existingNode.HierarchyPath.Length);
                    desc.HierarchyPath = $"{targetParent.HierarchyPath}{command.MemberToPlaceId}/{relative}";
                    desc.LastUpdateDate = now;
                    desc.LastUpdateBy   = _currentUser.UserId;
                }
            }

            var newPath = $"{targetParent.HierarchyPath}{command.MemberToPlaceId}/";
            var newNode = new DualTeamEntity
            {
                MemberId        = command.MemberToPlaceId,
                ParentMemberId  = command.TargetParentMemberId,
                Side            = side,
                HierarchyPath   = newPath,
                CreationDate    = now,
                CreatedBy       = _currentUser.UserId,
                LastUpdateDate  = now,
                LastUpdateBy    = _currentUser.UserId
            };

            _db.DualTeamTree.Add(newNode);

            var isFirstPlacement  = placementLog is null;
            var newOpportunities  = opportunitiesUsed + 1;

            var log = new PlacementLog
            {
                MemberId              = command.MemberToPlaceId,
                PlacedUnderMemberId   = command.TargetParentMemberId,
                Side                  = side,
                Action                = "Placed",
                Reason                = isAdmin ? "Admin placement" : "Ambassador placement",
                UnplacementCount      = newOpportunities,
                FirstPlacementDate    = isFirstPlacement ? now : placementLog!.FirstPlacementDate,
                CreationDate          = now,
                CreatedBy             = _currentUser.UserId
            };

            _db.PlacementLogs.Add(log);

            await _db.SaveChangesAsync(ct);

            // Sprint-15 Bug C: leg-points recalculation moved to the shared
            // IDualTeamPointsRecalculator so SignupAPI's PlaceMemberHandler can
            // call the same code path. Behavior here is unchanged.
            await _legPoints.RecalculateForUplinesAsync(command.TargetParentMemberId, ct);

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

    /// <summary>
    /// Plan P: las comprobaciones que dependen del estado del arbol, repetidas con el candado tomado. Null si
    /// todo sigue en orden. La lectura sin rastreo no toca las entidades que el manejador ya tiene cargadas.
    /// </summary>
    private async Task<Result<PlaceMemberResult>?> RecheckInsideLockAsync(
        PlaceMemberCommand command, TreeSide side, bool isMove, CancellationToken ct)
    {
        var target = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == command.TargetParentMemberId, ct);
        if (target is null)
            return Result<PlaceMemberResult>.Failure("TARGET_NOT_FOUND", "El nodo destino no existe en el Dual Team.");

        if (await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == command.TargetParentMemberId && d.Side == side, ct))
            return SlotOccupied(command);

        var node = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == command.MemberToPlaceId, ct);
        if ((node is not null) != isMove)
            return Result<PlaceMemberResult>.Failure("PLACEMENT_CONFLICT",
                "La posicion del miembro cambio mientras se colocaba. Vuelve a intentarlo.");
        if (node is not null && target.HierarchyPath.StartsWith(node.HierarchyPath))
            return Result<PlaceMemberResult>.Failure("CIRCULAR_REFERENCE",
                "No se puede realizar este placement: generaría una referencia circular en el árbol.");
        return null;
    }

    /// <summary>
    /// Checks if the member has any enrolled children currently in the dual tree
    /// within their subtree path.
    /// </summary>
    private async Task<bool> HasOwnEnrolledDownlineAsync(
        string memberId, string hierarchyPath, CancellationToken ct)
    {
        // Members enrolled by this ambassador
        var enrolledByThisMember = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == memberId && !m.IsDeleted)
            .Select(m => m.MemberId)
            .ToListAsync(ct);

        if (!enrolledByThisMember.Any()) return false;

        // Any of them currently in the subtree of this member
        return await _db.DualTeamTree
            .AnyAsync(d => enrolledByThisMember.Contains(d.MemberId)
                        && d.HierarchyPath.StartsWith(hierarchyPath)
                        && d.MemberId != memberId, ct);
    }
}
