using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Trees;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement;

/// <summary>
/// Admin removes a placement without time-window or opportunity restrictions.
/// Direct children are detached (become orphans). Ghost points are NOT transferred.
/// </summary>
public class AdminRemovePlacementHandler : IRequestHandler<AdminRemovePlacementCommand, Result<string>>
{
    private readonly AppDbContext               _db;
    private readonly ICurrentUserService        _currentUser;
    private readonly IDateTimeProvider          _clock;
    private readonly IDualTeamPointsRecalculator _legPoints;

    public AdminRemovePlacementHandler(
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
            // Keep all descendants linked to the removed member so that when the member
            // is re-placed, their subtree moves with them.
            // Replace the old path prefix with /{memberId}/ (member becomes a floating root).
            var oldPrefix = node.HierarchyPath;                    // e.g. /root/A/
            var floatPrefix = $"/{command.MemberId}/";             // e.g. /A/

            var allDescendants = await _db.DualTeamTree
                .Where(d => d.HierarchyPath.StartsWith(oldPrefix) && d.MemberId != command.MemberId)
                .ToListAsync(ct);

            foreach (var desc in allDescendants)
            {
                desc.HierarchyPath  = floatPrefix + desc.HierarchyPath.Substring(oldPrefix.Length);
                desc.LastUpdateDate = now;
                desc.LastUpdateBy   = _currentUser.UserId;
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

            // Sprint-15 follow-up — was a local CountSubtreeAsync that
            // CountAsync'd nodes instead of summing PersonalPoints (wrong
            // values). Delegate to the shared recalculator (single source of
            // truth used by SignupAPI + BizCenter).
            if (!string.IsNullOrEmpty(parentId))
                await _legPoints.RecalculateForUplinesAsync(parentId, ct);

            await tx.CommitAsync(ct);

            return Result<string>.Success("Placement eliminado exitosamente por el administrador.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
