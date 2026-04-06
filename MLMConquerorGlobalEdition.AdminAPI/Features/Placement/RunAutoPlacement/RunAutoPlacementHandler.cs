using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.RunAutoPlacement;

/// <summary>
/// Admin-triggered auto-placement run. Applies the same BFS rules as AutoPlacementJob.
/// Idempotent: only processes members not yet in the dual tree.
/// </summary>
public class RunAutoPlacementHandler : IRequestHandler<RunAutoPlacementCommand, Result<RunAutoPlacementResult>>
{
    private const int PlacementWindowDays = 30;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public RunAutoPlacementHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public async Task<Result<RunAutoPlacementResult>> Handle(
        RunAutoPlacementCommand request, CancellationToken ct)
    {
        var now          = _clock.Now;
        var windowCutoff = now.AddDays(-PlacementWindowDays);

        // Members whose window has expired and are NOT in the dual tree
        var unplaced = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted
                     && m.EnrollDate < windowCutoff
                     && m.SponsorMemberId != null)
            .ToListAsync(ct);

        var unplacedIds = unplaced.Select(m => m.MemberId).ToList();
        if (!unplacedIds.Any())
            return Result<RunAutoPlacementResult>.Success(
                new RunAutoPlacementResult(0, Enumerable.Empty<string>()));

        var alreadyInTree = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => unplacedIds.Contains(d.MemberId))
            .Select(d => d.MemberId)
            .ToListAsync(ct);

        var toPlace = unplaced.Where(m => !alreadyInTree.Contains(m.MemberId)).ToList();
        if (!toPlace.Any())
            return Result<RunAutoPlacementResult>.Success(
                new RunAutoPlacementResult(0, Enumerable.Empty<string>()));

        var placedIds  = new List<string>();
        var executorId = _currentUser.UserId;

        foreach (var member in toPlace)
        {
            var sponsorNode = await _db.DualTeamTree
                .FirstOrDefaultAsync(d => d.MemberId == member.SponsorMemberId, ct);

            if (sponsorNode is null) continue;

            (string? targetParentId, TreeSide side) = await DetermineAutoPlacementPositionAsync(
                member.SponsorMemberId!, sponsorNode, ct);

            if (targetParentId is null) continue;

            var targetParent = await _db.DualTeamTree
                .FirstOrDefaultAsync(d => d.MemberId == targetParentId, ct);

            if (targetParent is null) continue;

            var newPath = $"{targetParent.HierarchyPath}{member.MemberId}/";

            _db.DualTeamTree.Add(new DualTeamEntity
            {
                MemberId       = member.MemberId,
                ParentMemberId = targetParentId,
                Side           = side,
                HierarchyPath  = newPath,
                CreationDate   = now,
                CreatedBy      = executorId,
                LastUpdateDate = now,
                LastUpdateBy   = executorId
            });

            _db.PlacementLogs.Add(new PlacementLog
            {
                MemberId            = member.MemberId,
                PlacedUnderMemberId = targetParentId,
                Side                = side,
                Action              = "AutoPlaced",
                Reason              = "Admin-triggered auto-placement",
                UnplacementCount    = 0,
                FirstPlacementDate  = now,
                CreationDate        = now,
                CreatedBy           = executorId
            });

            placedIds.Add(member.MemberId);
        }

        await _db.SaveChangesAsync(ct);
        return Result<RunAutoPlacementResult>.Success(
            new RunAutoPlacementResult(placedIds.Count, placedIds));
    }

    private async Task<(string? TargetParentId, TreeSide Side)> DetermineAutoPlacementPositionAsync(
        string sponsorId, DualTeamEntity sponsorNode, CancellationToken ct)
    {
        var leftChild = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ParentMemberId == sponsorId && d.Side == TreeSide.Left, ct);

        var rightChild = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ParentMemberId == sponsorId && d.Side == TreeSide.Right, ct);

        // 5.2.a: No children → place LEFT of sponsor
        if (leftChild is null)
            return (sponsorId, TreeSide.Left);

        // 5.2.b: Only left child → place RIGHT of sponsor
        if (rightChild is null)
            return (sponsorId, TreeSide.Right);

        // 5.2.c: Both children → BFS on preferred side
        var preferredSide = sponsorNode.Side;
        var (bfsTarget, bfsSide) = await FindDeepestAvailableNodeAsync(sponsorId, preferredSide, ct);

        if (bfsTarget is not null) return (bfsTarget, bfsSide);

        // Fallback: try opposite side
        var otherSide = preferredSide == TreeSide.Left ? TreeSide.Right : TreeSide.Left;
        var (bfsTarget2, bfsSide2) = await FindDeepestAvailableNodeAsync(sponsorId, otherSide, ct);

        return (bfsTarget2, bfsSide2);
    }

    private async Task<(string? TargetParentId, TreeSide Side)> FindDeepestAvailableNodeAsync(
        string rootId, TreeSide preferredSide, CancellationToken ct)
    {
        var queue = new Queue<string>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            var leftChild = await _db.DualTeamTree
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ParentMemberId == current && d.Side == TreeSide.Left, ct);

            var rightChild = await _db.DualTeamTree
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ParentMemberId == current && d.Side == TreeSide.Right, ct);

            if (preferredSide == TreeSide.Left && leftChild is null)
                return (current, TreeSide.Left);

            if (preferredSide == TreeSide.Right && rightChild is null)
                return (current, TreeSide.Right);

            if (leftChild is not null)  queue.Enqueue(leftChild.MemberId);
            if (rightChild is not null) queue.Enqueue(rightChild.MemberId);
        }

        return (null, TreeSide.Left);
    }
}
