using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement;

/// <summary>
/// Admin removes a placement without time-window or opportunity restrictions.
/// Direct children are detached (become orphans). Ghost points are NOT transferred.
/// </summary>
public class AdminRemovePlacementHandler : IRequestHandler<AdminRemovePlacementCommand, Result<string>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public AdminRemovePlacementHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public async Task<Result<string>> Handle(AdminRemovePlacementCommand command, CancellationToken ct)
    {
        var now = _clock.Now;

        var node = await _db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == command.MemberId, ct);

        if (node is null)
            return Result<string>.Failure("NOT_PLACED", "El miembro no tiene placement activo.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Detach all direct children (they become tree roots)
            var directChildren = await _db.DualTeamTree
                .Where(d => d.ParentMemberId == command.MemberId)
                .ToListAsync(ct);

            foreach (var child in directChildren)
            {
                child.ParentMemberId = null;
                child.HierarchyPath  = $"/{child.MemberId}/";
                child.LastUpdateDate = now;
                child.LastUpdateBy   = _currentUser.UserId;
            }

            // Update grandchildren hierarchy paths
            foreach (var child in directChildren)
            {
                var grandchildren = await _db.DualTeamTree
                    .Where(d => d.HierarchyPath.StartsWith(node.HierarchyPath + child.MemberId + "/"))
                    .ToListAsync(ct);

                foreach (var gc in grandchildren)
                {
                    gc.HierarchyPath  = gc.HierarchyPath.Replace(
                        node.HierarchyPath, "/");
                    gc.LastUpdateDate = now;
                    gc.LastUpdateBy   = _currentUser.UserId;
                }
            }

            var parentId = node.ParentMemberId;
            _db.DualTeamTree.Remove(node);

            // Log the removal
            var prevLog = await _db.PlacementLogs
                .Where(p => p.MemberId == command.MemberId)
                .OrderByDescending(p => p.CreationDate)
                .FirstOrDefaultAsync(ct);

            _db.PlacementLogs.Add(new PlacementLog
            {
                MemberId            = command.MemberId,
                PlacedUnderMemberId = string.Empty,
                Side                = Domain.Enums.TreeSide.Left, // sentinel — removal has no target side
                Action              = "Removed",
                Reason              = "Admin removal",
                UnplacementCount    = prevLog?.UnplacementCount ?? 0, // admin does not increment
                FirstPlacementDate  = prevLog?.FirstPlacementDate,
                CreationDate        = now,
                CreatedBy           = _currentUser.UserId
            });

            await _db.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(parentId))
                await RecalculateUplineStatsAsync(parentId, ct);

            await tx.CommitAsync(ct);

            return Result<string>.Success("Placement eliminado exitosamente por el administrador.");
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

            node.LeftLegPoints  = await CountSubtreeAsync(current, Domain.Enums.TreeSide.Left, ct);
            node.RightLegPoints = await CountSubtreeAsync(current, Domain.Enums.TreeSide.Right, ct);
            node.LastUpdateDate = _clock.Now;
            node.LastUpdateBy   = "admin-system";

            current = node.ParentMemberId;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<decimal> CountSubtreeAsync(
        string parentMemberId, Domain.Enums.TreeSide side, CancellationToken ct)
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
