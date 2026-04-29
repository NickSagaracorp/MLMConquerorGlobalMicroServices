using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.RemovePlacement;

public class RemovePlacementHandler : IRequestHandler<RemovePlacementCommand, Result<RemovePlacementResult>>
{
    private const int CorrectionWindowHours     = 72;
    private const int MaxPlacementOpportunities = 2;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public RemovePlacementHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public async Task<Result<RemovePlacementResult>> Handle(
        RemovePlacementCommand command, CancellationToken ct)
    {
        var now     = _clock.UtcNow;
        var isAdmin = _currentUser.IsAdmin;

        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == command.MemberToRemoveId && !m.IsDeleted, ct);

        if (member is null)
            return Result<RemovePlacementResult>.Failure("MEMBER_NOT_FOUND",
                "El miembro no existe.");

        var node = await _db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == command.MemberToRemoveId, ct);

        if (node is null)
            return Result<RemovePlacementResult>.Failure("NOT_PLACED",
                "El miembro no tiene placement asignado en el Dual Team.");

        var log = await _db.PlacementLogs
            .Where(p => p.MemberId == command.MemberToRemoveId)
            .OrderByDescending(p => p.CreationDate)
            .FirstOrDefaultAsync(ct);

        var opportunitiesUsed = log?.UnplacementCount ?? 0;

        if (!isAdmin)
        {
            if (opportunitiesUsed >= MaxPlacementOpportunities)
                throw new UnplacementLimitExceededException();

            if (log?.FirstPlacementDate is null ||
                now > log.FirstPlacementDate.Value.AddHours(CorrectionWindowHours))
                throw new UnplacementWindowExpiredException();
        }

        var parentMemberId = node.ParentMemberId;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.DualTeamTree.Remove(node);

            var directChildren = await _db.DualTeamTree
                .Where(d => d.ParentMemberId == command.MemberToRemoveId)
                .ToListAsync(ct);

            foreach (var child in directChildren)
            {
                child.ParentMemberId = null;
                child.HierarchyPath  = $"/{child.MemberId}/";
                child.LastUpdateDate = now;
                child.LastUpdateBy   = _currentUser.UserId;
            }

            var removalLog = new PlacementLog
            {
                MemberId            = command.MemberToRemoveId,
                PlacedUnderMemberId = parentMemberId ?? string.Empty,
                Side                = node.Side,
                Action              = "Removed",
                Reason              = isAdmin ? "Admin removal" : "Ambassador correction",
                UnplacementCount    = opportunitiesUsed + 1,
                FirstPlacementDate  = log?.FirstPlacementDate,
                CreationDate        = now,
                CreatedBy           = _currentUser.UserId
            };

            _db.PlacementLogs.Add(removalLog);
            await _db.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(parentMemberId))
                await RecalculateUplineAsync(parentMemberId, ct);

            await tx.CommitAsync(ct);

            var remaining = Math.Max(0, MaxPlacementOpportunities - (opportunitiesUsed + 1));
            return Result<RemovePlacementResult>.Success(new RemovePlacementResult(
                command.MemberToRemoveId,
                $"{member.FirstName} {member.LastName}",
                remaining
            ));
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task RecalculateUplineAsync(string startMemberId, CancellationToken ct)
    {
        var current = startMemberId;
        while (!string.IsNullOrEmpty(current))
        {
            var n = await _db.DualTeamTree.FirstOrDefaultAsync(d => d.MemberId == current, ct);
            if (n is null) break;

            var leftTotal  = await SumSubtreePointsAsync(current, Domain.Enums.TreeSide.Left,  ct);
            var rightTotal = await SumSubtreePointsAsync(current, Domain.Enums.TreeSide.Right, ct);
            n.LeftLegPoints  = leftTotal;
            n.RightLegPoints = rightTotal;
            n.LastUpdateDate = _clock.UtcNow;
            n.LastUpdateBy   = "system";

            var stats = await _db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == current, ct);
            if (stats is not null)
                stats.DualTeamPoints = (int)(leftTotal + rightTotal);

            current = n.ParentMemberId;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sum of PersonalPoints across the leg subtree (including the leg-root member).
    /// </summary>
    private async Task<decimal> SumSubtreePointsAsync(
        string parentId, Domain.Enums.TreeSide side, CancellationToken ct)
    {
        var child = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ParentMemberId == parentId && d.Side == side, ct);

        if (child is null) return 0m;

        var total = await (
            from d in _db.DualTeamTree.AsNoTracking()
            join s in _db.MemberStatistics.AsNoTracking() on d.MemberId equals s.MemberId
            where d.HierarchyPath.StartsWith(child.HierarchyPath)
            select (decimal?)s.PersonalPoints
        ).SumAsync(ct);

        return total ?? 0m;
    }
}
