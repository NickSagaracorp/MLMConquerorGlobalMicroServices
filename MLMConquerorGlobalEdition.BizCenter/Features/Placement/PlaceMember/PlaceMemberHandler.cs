using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;

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

            await RecalculateUplineStatsAsync(command.TargetParentMemberId, ct);

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
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
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

    /// <summary>
    /// Walks up the Dual Team from the given node and recalculates
    /// LeftLegPoints / RightLegPoints for each ancestor.
    /// Ghost points are NOT transferred — only organic tree points.
    /// </summary>
    private async Task RecalculateUplineStatsAsync(string startMemberId, CancellationToken ct)
    {
        var current = startMemberId;
        while (!string.IsNullOrEmpty(current))
        {
            var node = await _db.DualTeamTree
                .FirstOrDefaultAsync(d => d.MemberId == current, ct);

            if (node is null) break;

            // Sum points of all members in left subtree (excluding ghost points)
            var leftTotal = await SumSubtreePointsAsync(current, TreeSide.Left, ct);
            var rightTotal = await SumSubtreePointsAsync(current, TreeSide.Right, ct);

            node.LeftLegPoints  = leftTotal;
            node.RightLegPoints = rightTotal;
            node.LastUpdateDate = _clock.UtcNow;
            node.LastUpdateBy   = "system";

            current = node.ParentMemberId;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<decimal> SumSubtreePointsAsync(
        string parentMemberId, TreeSide side, CancellationToken ct)
    {
        // Find immediate child on this side
        var child = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ParentMemberId == parentMemberId && d.Side == side, ct);

        if (child is null) return 0m;

        // Sum all downline members' active subscription points (placeholder — real logic
        // depends on CommissionEngine; here we count each member in subtree as 1 point)
        var subtreeCount = await _db.DualTeamTree
            .AsNoTracking()
            .CountAsync(d => d.HierarchyPath.StartsWith(child.HierarchyPath), ct);

        return subtreeCount;
    }
}
