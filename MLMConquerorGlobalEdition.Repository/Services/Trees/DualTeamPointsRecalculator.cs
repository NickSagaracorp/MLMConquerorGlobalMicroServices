using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Trees;

/// <inheritdoc />
public class DualTeamPointsRecalculator : IDualTeamPointsRecalculator
{
    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public DualTeamPointsRecalculator(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task RecalculateForUplinesAsync(string startMemberId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(startMemberId)) return;

        var now     = _clock.Now;
        var current = startMemberId;

        while (!string.IsNullOrEmpty(current))
        {
            var node = await _db.DualTeamTree
                .FirstOrDefaultAsync(d => d.MemberId == current, ct);

            if (node is null) break;

            var leftTotal  = await SumSubtreePointsAsync(current, TreeSide.Left,  ct);
            var rightTotal = await SumSubtreePointsAsync(current, TreeSide.Right, ct);

            node.LeftLegPoints  = leftTotal;
            node.RightLegPoints = rightTotal;
            node.LastUpdateDate = now;
            node.LastUpdateBy   = "system";

            // Mirror onto MemberStatistics so ranks/dashboards see the same number.
            var stats = await _db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == current, ct);
            if (stats is not null)
                stats.DualTeamPoints = (int)(leftTotal + rightTotal);

            current = node.ParentMemberId;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sum of <see cref="Domain.Entities.Member.MemberStatisticEntity.PersonalPoints"/>
    /// for every member in the subtree on the given side, INCLUDING the immediate
    /// leg-root member. Returns 0 when the side is empty.
    /// </summary>
    private async Task<decimal> SumSubtreePointsAsync(
        string parentMemberId, TreeSide side, CancellationToken ct)
    {
        var child = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ParentMemberId == parentMemberId && d.Side == side, ct);

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
