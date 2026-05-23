using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
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

        var side = Enum.Parse<TreeSide>(command.Side);

        // Sprint-15 Bug B: instead of descending the same side recursively (the
        // old algorithm built a 100+ deep chain that overflowed the
        // HierarchyPath index), BFS the requested sponsor's subtree for the
        // shallowest node with the matching side slot still open. This keeps
        // the tree wide rather than deep.
        var slot = await FindFirstEmptySlotByBfsAsync(command.PlaceUnderMemberId, side, ct);
        if (slot is null)
            return Result<bool>.Failure(
                "NO_AVAILABLE_SLOT",
                $"No available {command.Side} slot found under '{command.PlaceUnderMemberId}' " +
                $"(all candidates were either occupied or near the 1500-byte HierarchyPath safety cap).");

        var (parentMemberId, parentPath) = slot.Value;
        var newPath = $"{parentPath}{command.MemberId}/";

        var node = new DualTeamEntity
        {
            MemberId       = command.MemberId,
            ParentMemberId = parentMemberId,
            Side           = side,
            HierarchyPath  = newPath,
            CreatedBy      = command.MemberId,
            CreationDate   = now,
            LastUpdateDate = now
        };

        var log = new PlacementLog
        {
            MemberId            = command.MemberId,
            PlacedUnderMemberId = parentMemberId,
            Side                = side,
            Action              = "Placed",
            FirstPlacementDate  = now,
            CreationDate        = now,
            CreatedBy           = command.MemberId
        };

        await _db.DualTeamTree.AddAsync(node, ct);
        await _db.PlacementLogs.AddAsync(log, ct);

        // Queue rank re-evaluation for every dual-team upline of the placement parent
        var dualUplineIds = parentPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => id != command.MemberId);

        foreach (var uplineId in dualUplineIds)
        {
            await _db.RankEvaluationQueue.AddAsync(new RankEvaluationQueue
            {
                TriggerMemberId  = command.MemberId,
                EvaluateMemberId = uplineId,
                TriggerEvent     = RankEvaluationTrigger.Placement,
                TriggerDate      = now,
                CreatedBy        = command.MemberId,
                CreationDate     = now
            }, ct);
        }

        await _db.SaveChangesAsync(ct);

        // Sprint-15 Bug C: SignupAPI placements used to leave LeftLegPoints /
        // RightLegPoints stale. Recompute the binary leg sums up the new
        // parent's chain using the same shared service BizCenter uses.
        await _legPoints.RecalculateForUplinesAsync(parentMemberId, ct);

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

    /// <summary>
    /// Sprint-15 Bug B — Breadth-first search of the binary subtree rooted at
    /// <paramref name="rootMemberId"/>, returning the first node with an open
    /// slot on <paramref name="requiredSide"/>. Pulling the entire subtree once
    /// is cheaper than walking it node by node when subtrees are wide, and the
    /// 1500-byte guard keeps us from creating a HierarchyPath the SQL index
    /// can't store. Returns the parent-memberId + parent's HierarchyPath, or
    /// <c>null</c> when no usable slot exists.
    /// </summary>
    private async Task<(string ParentMemberId, string ParentHierarchyPath)?> FindFirstEmptySlotByBfsAsync(
        string rootMemberId, TreeSide requiredSide, CancellationToken ct)
    {
        // Bootstrap — root might not exist in the dual tree at all yet.
        // In that case the slot under it is trivially open and the path is
        // synthesized the same way the seeder does it.
        var rootNode = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == rootMemberId, ct);

        var rootPath = rootNode?.HierarchyPath ?? $"/{rootMemberId}/";

        // Direct child lookup — fast path when the requested slot is empty.
        var rootDirectOccupied = await _db.DualTeamTree.AnyAsync(
            d => d.ParentMemberId == rootMemberId && d.Side == requiredSide, ct);

        if (!rootDirectOccupied && rootPath.Length <= MaxParentHierarchyPathBytes)
            return (rootMemberId, rootPath);

        // Pull the whole subtree under the root in one shot, sorted by depth
        // (path length ascending = shallowest first). BFS over that snapshot.
        var subtree = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => d.HierarchyPath.StartsWith(rootPath))
            .Select(d => new SubtreeNode(d.MemberId, d.HierarchyPath, d.ParentMemberId, d.Side))
            .ToListAsync(ct);

        if (subtree.Count == 0) return null;

        var occupied = subtree
            .Where(n => n.ParentMemberId is not null)
            .GroupBy(n => n.ParentMemberId!)
            .ToDictionary(
                g => g.Key,
                g => new {
                    Left  = g.Any(x => x.Side == TreeSide.Left),
                    Right = g.Any(x => x.Side == TreeSide.Right)
                });

        // BFS — sort ascending by path length so the first match is the shallowest.
        // (Identical to the PowerShell backfill script in scripts/backfill-left.ps1.)
        foreach (var node in subtree.OrderBy(n => n.HierarchyPath.Length))
        {
            if (node.HierarchyPath.Length > MaxParentHierarchyPathBytes) continue;

            var slots = occupied.GetValueOrDefault(node.MemberId);
            var sideTaken = requiredSide == TreeSide.Left
                ? (slots?.Left  ?? false)
                : (slots?.Right ?? false);

            if (!sideTaken)
                return (node.MemberId, node.HierarchyPath);
        }

        return null;
    }

    private sealed record SubtreeNode(
        string MemberId,
        string HierarchyPath,
        string? ParentMemberId,
        TreeSide Side);
}
