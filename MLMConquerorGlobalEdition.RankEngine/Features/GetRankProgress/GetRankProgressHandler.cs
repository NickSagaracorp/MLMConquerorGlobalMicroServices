using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Mappings;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;

public class GetRankProgressHandler : IRequestHandler<GetRankProgressQuery, Result<RankProgressResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public GetRankProgressHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db = db;
        _dateTime = dateTime;
    }

    public async Task<Result<RankProgressResponse>> Handle(GetRankProgressQuery request, CancellationToken ct)
    {
        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == request.MemberId, ct);

        if (member is null)
            return Result<RankProgressResponse>.Failure("MEMBER_NOT_FOUND", $"Member '{request.MemberId}' not found.");

        // Get current rank
        var currentRankHistory = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(h => h.RankDefinition)
            .ThenInclude(r => r!.Requirements)
            .Where(h => h.MemberId == request.MemberId && !h.IsDeleted)
            .OrderByDescending(h => h.RankDefinition!.SortOrder)
            .FirstOrDefaultAsync(ct);

        // Get all active ranks ordered by SortOrder
        var allRanks = await _db.RankDefinitions
            .AsNoTracking()
            .Include(r => r.Requirements)
            .Where(r => r.Status == RankDefinitionStatus.Active)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var currentSortOrder = currentRankHistory?.RankDefinition?.SortOrder ?? 0;
        var nextRank = allRanks.FirstOrDefault(r => r.SortOrder > currentSortOrder);

        var metrics = await ComputeMetricsAsync(request.MemberId, ct);

        RankThresholdProgress? progressToNext = null;
        if (nextRank is not null && nextRank.Requirements.Count > 0)
        {
            var req = nextRank.Requirements.OrderBy(r => r.LevelNo).First();
            progressToNext = ComputeProgress(metrics, req);
        }

        var response = new RankProgressResponse
        {
            MemberId = request.MemberId,
            CurrentRank = currentRankHistory?.RankDefinition is not null
                ? RankEngineMappingExtensions.ToResponse(currentRankHistory.RankDefinition)
                : null,
            NextRank = nextRank is not null ? RankEngineMappingExtensions.ToResponse(nextRank) : null,
            CurrentMetrics = metrics,
            ProgressToNextRank = progressToNext,
            EvaluatedAt = _dateTime.Now
        };

        return Result<RankProgressResponse>.Success(response);
    }

    internal async Task<RankMetricsResponse> ComputeMetricsAsync(string memberId, CancellationToken ct)
    {
        // Personal points: sum QualificationPoins from member's completed orders via LINQ join
        var personalPoints = await (
            from o in _db.Orders.AsNoTracking()
            join od in _db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
            join p in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where o.MemberId == memberId && o.Status == Domain.Entities.Orders.OrderStatus.Completed
            select p.QualificationPoins
        ).SumAsync(ct);

        // Team points from binary dual tree
        var dualNode = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);

        var leftPoints = dualNode?.LeftLegPoints ?? 0;
        var rightPoints = dualNode?.RightLegPoints ?? 0;
        var totalTeam = leftPoints + rightPoints;
        // Qualifying team points: cap each branch at 50% of total (weaker leg × 2)
        var qualifyingTeam = Math.Min(leftPoints, rightPoints) * 2;

        // Enrollment tree count (direct downline + subtree via HierarchyPath prefix)
        var memberGenealogyNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);

        var enrollmentCount = 0;
        var placementQualifiedCount = 0;
        var enrollmentQualifiedCount = 0;

        if (memberGenealogyNode is not null)
        {
            var subtreePath = memberGenealogyNode.HierarchyPath;
            enrollmentCount = await _db.GenealogyTree
                .AsNoTracking()
                .CountAsync(g => g.HierarchyPath.StartsWith(subtreePath) && g.MemberId != memberId, ct);

            // Qualified = members with at least one active subscription in subtree
            var subtreeMembers = await _db.GenealogyTree
                .AsNoTracking()
                .Where(g => g.HierarchyPath.StartsWith(subtreePath) && g.MemberId != memberId)
                .Select(g => g.MemberId)
                .ToListAsync(ct);

            enrollmentQualifiedCount = await _db.MembershipSubscriptions
                .AsNoTracking()
                .CountAsync(s => subtreeMembers.Contains(s.MemberId)
                    && s.SubscriptionStatus == Domain.Entities.Membership.MembershipStatus.Active, ct);
        }

        // Placement qualified members in dual tree subtree
        if (dualNode is not null)
        {
            var dualSubtreePath = dualNode.HierarchyPath;
            var dualSubtreeMembers = await _db.DualTeamTree
                .AsNoTracking()
                .Where(d => d.HierarchyPath.StartsWith(dualSubtreePath) && d.MemberId != memberId)
                .Select(d => d.MemberId)
                .ToListAsync(ct);

            placementQualifiedCount = await _db.MembershipSubscriptions
                .AsNoTracking()
                .CountAsync(s => dualSubtreeMembers.Contains(s.MemberId)
                    && s.SubscriptionStatus == Domain.Entities.Membership.MembershipStatus.Active, ct);
        }

        // Direct sponsored members
        var directSponsored = await _db.MemberProfiles
            .AsNoTracking()
            .CountAsync(m => m.SponsorMemberId == memberId, ct);

        // External members (ExternalMember type) in direct sponsored
        var externalMembers = await _db.MemberProfiles
            .AsNoTracking()
            .CountAsync(m => m.SponsorMemberId == memberId && m.MemberType == MemberType.ExternalMember, ct);

        // Sales volume: sum of completed orders
        var salesVolume = await _db.Orders
            .AsNoTracking()
            .Where(o => o.MemberId == memberId && o.Status == Domain.Entities.Orders.OrderStatus.Completed)
            .SumAsync(o => (decimal?)o.TotalAmount ?? 0, ct);

        return new RankMetricsResponse
        {
            PersonalPoints = personalPoints,
            TotalTeamPoints = totalTeam,
            LeftLegPoints = leftPoints,
            RightLegPoints = rightPoints,
            QualifyingTeamPoints = qualifyingTeam,
            EnrollmentTeamCount = enrollmentCount,
            PlacementQualifiedTeamMembers = placementQualifiedCount,
            EnrollmentQualifiedTeamMembers = enrollmentQualifiedCount,
            SponsoredMembers = directSponsored,
            ExternalMembers = externalMembers,
            SalesVolume = salesVolume
        };
    }

    internal static RankThresholdProgress ComputeProgress(RankMetricsResponse metrics, Domain.Entities.Rank.RankRequirement req)
    {
        static double Pct(double actual, double threshold)
            => threshold <= 0 ? 100.0 : Math.Min(100.0, actual / threshold * 100.0);

        var p1 = Pct(metrics.PersonalPoints, req.PersonalPoints);
        var p2 = Pct((double)metrics.QualifyingTeamPoints, req.TeamPoints);
        var p3 = Pct(metrics.EnrollmentTeamCount, req.EnrollmentTeam);
        var p4 = Pct(metrics.SponsoredMembers, req.SponsoredMembers);
        var p5 = Pct(metrics.ExternalMembers, req.ExternalMembers);
        var p6 = Pct((double)metrics.SalesVolume, (double)req.SalesVolume);

        var activeThresholds = new List<double>();
        if (req.PersonalPoints > 0) activeThresholds.Add(p1);
        if (req.TeamPoints > 0) activeThresholds.Add(p2);
        if (req.EnrollmentTeam > 0) activeThresholds.Add(p3);
        if (req.SponsoredMembers > 0) activeThresholds.Add(p4);
        if (req.ExternalMembers > 0) activeThresholds.Add(p5);
        if (req.SalesVolume > 0) activeThresholds.Add(p6);

        var overall = activeThresholds.Count > 0 ? activeThresholds.Average() : 100.0;

        return new RankThresholdProgress
        {
            PersonalPointsPercent = Math.Round(p1, 2),
            TeamPointsPercent = Math.Round(p2, 2),
            EnrollmentTeamPercent = Math.Round(p3, 2),
            SponsoredMembersPercent = Math.Round(p4, 2),
            ExternalMembersPercent = Math.Round(p5, 2),
            SalesVolumePercent = Math.Round(p6, 2),
            OverallPercent = Math.Round(overall, 2)
        };
    }
}
