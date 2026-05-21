using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <inheritdoc />
public sealed class EnrollmentTeamPointsService : IEnrollmentTeamPointsService
{
    private readonly AppDbContext _db;

    public EnrollmentTeamPointsService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EnrollmentBranchPoints>> GetEnrollmentBranchPointsAsync(
        string memberId, CancellationToken ct = default)
    {
        var childIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (childIds.Count == 0)
            return Array.Empty<EnrollmentBranchPoints>();

        var stats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => childIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);

        var byMember = stats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);
        return childIds
            .Select(id => new EnrollmentBranchPoints(id, byMember.GetValueOrDefault(id, 0)))
            .ToList();
    }

    public async Task<int> GetRawEnrollmentTeamPointsAsync(string memberId, CancellationToken ct = default)
    {
        var branches = await GetEnrollmentBranchPointsAsync(memberId, ct);
        return branches.Sum(b => b.BranchPoints);
    }

    public async Task<int> GetEligibleEnrollmentTeamPointsAsync(
        string memberId, RankRequirement requirement, CancellationToken ct = default)
    {
        if (requirement.EnrollmentTeam <= 0)
            return 0;

        var branches = await GetEnrollmentBranchPointsAsync(memberId, ct);

        var perBranchCap = requirement.MaxEnrollmentTeamPointsPerBranch > 0
            ? (int)Math.Round(requirement.MaxEnrollmentTeamPointsPerBranch * requirement.EnrollmentTeam)
            : 0;

        var summed = perBranchCap > 0
            ? branches.Sum(b => Math.Min(b.BranchPoints, perBranchCap))
            : branches.Sum(b => b.BranchPoints);

        return Math.Min(summed, requirement.EnrollmentTeam);
    }

    public async Task<int> RecomputeEnrollmentPointsAsync(string memberId, CancellationToken ct = default)
    {
        var node = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);
        if (node is null)
            return 0;

        // EnrollmentPoints = the member's OWN points + every downline member's points.
        // So the recompute sums completed-order points over the whole subtree
        // INCLUDING the member themselves.
        var subtreeIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(node.HierarchyPath))
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        return await (
            from o in _db.Orders.AsNoTracking()
            join od in _db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
            join p in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where subtreeIds.Contains(o.MemberId) && o.Status == OrderStatus.Completed
            select p.QualificationPoins
        ).SumAsync(ct);
    }
}
