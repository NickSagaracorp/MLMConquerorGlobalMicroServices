using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminPlaceMember;

/// <summary>
/// Admin placement bypasses time and opportunity restrictions but still enforces
/// structural rules: no circular reference, no third leg, no auto-superiority.
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

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == command.MemberToPlaceId && !m.IsDeleted, ct);

        if (member is null)
            return Result<string>.Failure("MEMBER_NOT_FOUND", "El miembro que intenta colocar no existe.");

        var targetParent = await _db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == command.TargetParentMemberId, ct);

        if (targetParent is null)
            return Result<string>.Failure("TARGET_NOT_FOUND", "El nodo destino no existe en el Dual Team.");

        var slotOccupied = await _db.DualTeamTree
            .AnyAsync(d => d.ParentMemberId == command.TargetParentMemberId && d.Side == side, ct);

        if (slotOccupied)
            return Result<string>.Failure("SLOT_OCCUPIED",
                $"El nodo {command.TargetParentMemberId} ya tiene ocupada la pierna {command.Side}.");

        if (command.TargetParentMemberId == command.MemberToPlaceId)
            return Result<string>.Failure("AUTO_SUPERIORITY",
                "Un ambassador no puede ser su propio superior directo.");

        var existingNode = await _db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == command.MemberToPlaceId, ct);

        if (existingNode != null &&
            targetParent.HierarchyPath.StartsWith(existingNode.HierarchyPath))
            return Result<string>.Failure("CIRCULAR_REFERENCE",
                "No se puede realizar este placement: generaría una referencia circular en el árbol.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (existingNode != null)
            {
                _db.DualTeamTree.Remove(existingNode);

                var descendants = await _db.DualTeamTree
                    .Where(d => d.HierarchyPath.StartsWith(existingNode.HierarchyPath)
                             && d.MemberId != command.MemberToPlaceId)
                    .ToListAsync(ct);

                foreach (var desc in descendants)
                {
                    var relative    = desc.HierarchyPath.Substring(existingNode.HierarchyPath.Length);
                    desc.HierarchyPath  = $"{targetParent.HierarchyPath}{command.MemberToPlaceId}/{relative}";
                    desc.LastUpdateDate = now;
                    desc.LastUpdateBy   = _currentUser.UserId;
                }
            }

            var newPath = $"{targetParent.HierarchyPath}{command.MemberToPlaceId}/";
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

            await RecalculateUplineStatsAsync(command.TargetParentMemberId, ct);

            await tx.CommitAsync(ct);

            return Result<string>.Success(
                $"Placement de {member.FirstName} {member.LastName} completado exitosamente.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task RecalculateUplineStatsAsync(string startMemberId, CancellationToken ct)
    {
        var current = startMemberId;
        while (!string.IsNullOrEmpty(current))
        {
            var node = await _db.DualTeamTree
                .FirstOrDefaultAsync(d => d.MemberId == current, ct);

            if (node is null) break;

            node.LeftLegPoints  = await SumSubtreePointsAsync(current, TreeSide.Left, ct);
            node.RightLegPoints = await SumSubtreePointsAsync(current, TreeSide.Right, ct);
            node.LastUpdateDate = _clock.Now;
            node.LastUpdateBy   = "admin-system";

            current = node.ParentMemberId;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<decimal> SumSubtreePointsAsync(
        string parentMemberId, TreeSide side, CancellationToken ct)
    {
        var child = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ParentMemberId == parentMemberId && d.Side == side, ct);

        if (child is null) return 0m;

        return await _db.DualTeamTree
            .AsNoTracking()
            .CountAsync(d => d.HierarchyPath.StartsWith(child.HierarchyPath), ct);
    }
}
