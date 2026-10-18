using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Trees;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminPlaceMember;

/// <summary>
/// Admin placement bypasses time and opportunity restrictions but still enforces
/// structural rules: no circular reference, no third leg, no auto-superiority.
/// </summary>
public class AdminPlaceMemberHandler : IRequestHandler<AdminPlaceMemberCommand, Result<string>>
{
    private readonly AppDbContext               _db;
    private readonly ICurrentUserService        _currentUser;
    private readonly IDateTimeProvider          _clock;
    private readonly IDualTeamPointsRecalculator _legPoints;

    public AdminPlaceMemberHandler(
        AppDbContext               db,
        ICurrentUserService        currentUser,
        IDateTimeProvider          clock,
        IDualTeamPointsRecalculator legPoints)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
        _legPoints   = legPoints;
    }

    public async Task<Result<string>> Handle(AdminPlaceMemberCommand command, CancellationToken ct)
    {
        var now  = _clock.Now;
        var side = Enum.Parse<TreeSide>(command.Side);

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == command.MemberToPlaceId && !m.IsDeleted, ct);

        if (member is null)
            return Result<string>.Failure("MEMBER_NOT_FOUND", "El miembro que intenta colocar no existe.");

        var targetParent = await _db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == command.TargetParentMemberId, ct);

        var slotOccupied = targetParent is not null && await _db.DualTeamTree
            .AnyAsync(d => d.ParentMemberId == command.TargetParentMemberId && d.Side == side, ct);

        if (slotOccupied)
            return Result<string>.Failure("SLOT_OCCUPIED",
                $"El nodo {command.TargetParentMemberId} ya tiene ocupada la pierna {command.Side}.");

        if (command.TargetParentMemberId == command.MemberToPlaceId)
            return Result<string>.Failure("AUTO_SUPERIORITY",
                "Un ambassador no puede ser su propio superior directo.");

        var existingNode = await _db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == command.MemberToPlaceId, ct);

        // Circular reference: the target must not be under the member being placed,
        // whether the member is currently in the tree or is a floating root (was unplaced).
        var memberEffectivePath = existingNode?.HierarchyPath ?? $"/{command.MemberToPlaceId}/";
        if (targetParent is not null &&
            targetParent.HierarchyPath.StartsWith(memberEffectivePath))
            return Result<string>.Failure("CIRCULAR_REFERENCE",
                "No se puede realizar este placement: generaría una referencia circular en el árbol.");

        // Plan P: si se mueve un subarbol (el miembro esta en el arbol o tiene descendientes flotando bajo
        // /{miembro}/) se reescriben rutas y se toma el arbol en exclusivo; un alta simple toma solo su hueco.
        var floatPrefixCheck = $"/{command.MemberToPlaceId}/";
        var rewritesPaths = existingNode is not null
            || await _db.DualTeamTree.AnyAsync(d => d.HierarchyPath.StartsWith(floatPrefixCheck), ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (rewritesPaths)
                await DualTeamGate.EnterTreeExclusiveAsync(_db, ct);
            else
                await DualTeamGate.EnterSlotAsync(_db, command.TargetParentMemberId, side, ct);

            // Con el candado: el hueco y las rutas se vuelven a leer (la comprobacion de arriba es solo un
            // atajo; otro escritor pudo ocuparlo o mover el destino mientras tanto).
            if (await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == command.TargetParentMemberId && d.Side == side, ct))
            {
                await tx.RollbackAsync(ct);
                return SlotOccupied(command);
            }
            if (targetParent is not null) await _db.Entry(targetParent).ReloadAsync(ct);
            if (existingNode is not null) await _db.Entry(existingNode).ReloadAsync(ct);
            if (targetParent is not null && targetParent.HierarchyPath.StartsWith(existingNode?.HierarchyPath ?? floatPrefixCheck))
            {
                await tx.RollbackAsync(ct);
                return Result<string>.Failure("CIRCULAR_REFERENCE",
                    "No se puede realizar este placement: generaría una referencia circular en el árbol.");
            }

            // Auto-create the parent as a root node if it doesn't exist in the dual tree yet.
            // This happens on first placement under an ambassador who was never placed themselves.
            if (targetParent is null)
            {
                targetParent = new DualTeamEntity
                {
                    MemberId       = command.TargetParentMemberId,
                    ParentMemberId = null,
                    Side           = TreeSide.Left,
                    HierarchyPath  = $"/{command.TargetParentMemberId}/",
                    CreationDate   = now,
                    CreatedBy      = _currentUser.UserId,
                    LastUpdateDate = now,
                    LastUpdateBy   = _currentUser.UserId
                };
                _db.DualTeamTree.Add(targetParent);
                await _db.SaveChangesAsync(ct);
            }

            var newPath = $"{targetParent.HierarchyPath}{command.MemberToPlaceId}/";

            if (existingNode != null)
            {
                // Member is currently in the tree — move their whole subtree to the new position.
                _db.DualTeamTree.Remove(existingNode);

                var descendants = await _db.DualTeamTree
                    .Where(d => d.HierarchyPath.StartsWith(existingNode.HierarchyPath)
                             && d.MemberId != command.MemberToPlaceId)
                    .ToListAsync(ct);

                foreach (var desc in descendants)
                {
                    var relative   = desc.HierarchyPath.Substring(existingNode.HierarchyPath.Length);
                    desc.HierarchyPath  = newPath + relative;
                    desc.LastUpdateDate = now;
                    desc.LastUpdateBy   = _currentUser.UserId;
                }
            }
            else
            {
                // Member was previously unplaced — their descendants are floating under /{memberId}/.
                // Re-attach them to the new position.
                var floatPrefix = $"/{command.MemberToPlaceId}/";
                var floatingDescendants = await _db.DualTeamTree
                    .Where(d => d.HierarchyPath.StartsWith(floatPrefix))
                    .ToListAsync(ct);

                foreach (var desc in floatingDescendants)
                {
                    var relative   = desc.HierarchyPath.Substring(floatPrefix.Length);
                    desc.HierarchyPath  = newPath + relative;
                    desc.LastUpdateDate = now;
                    desc.LastUpdateBy   = _currentUser.UserId;
                }
            }
            _db.DualTeamTree.Add(new DualTeamEntity
            {
                MemberId       = command.MemberToPlaceId,
                ParentMemberId = command.TargetParentMemberId,
                Side           = side,
                HierarchyPath  = newPath,
                CreationDate   = now,
                CreatedBy      = _currentUser.UserId,
                LastUpdateDate = now,
                LastUpdateBy   = _currentUser.UserId
            });

            var prevLog = await _db.PlacementLogs
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

            // Sprint-15 follow-up — was a local SumSubtreePointsAsync that
            // CountAsync'd nodes instead of summing PersonalPoints (wrong
            // values). Delegate to the shared recalculator (single source of
            // truth used by SignupAPI + BizCenter).
            await _legPoints.RecalculateForUplinesAsync(command.TargetParentMemberId, ct);

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
}
