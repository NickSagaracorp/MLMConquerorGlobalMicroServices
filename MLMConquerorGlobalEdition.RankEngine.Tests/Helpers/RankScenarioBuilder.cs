using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Seeders;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;

/// <summary>
/// Builds an in-memory scenario in which a subject member exactly satisfies one rank's
/// RankRequirement plus the universal gate. All rows are tagged CreatedBy = "rank-validation"
/// with MemberId prefix "RVH-" so future cleanup can remove them.
/// </summary>
public sealed class RankScenarioBuilder
{
    public const string Tag = "rank-validation";
    public const string MemberPrefix = "RVH-";

    private static readonly DateTime Now = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);
    private readonly AppDbContext _db;
    private readonly string _runId = Guid.NewGuid().ToString("N")[..6];
    private int _seq;

    public RankScenarioBuilder(AppDbContext db) => _db = db;

    /// <summary>
    /// Builds the scenario for one rank and returns the subject's MemberId.
    /// <paramref name="thresholdDelta"/> is added to every DT and ET point threshold — pass -1 to
    /// build a boundary scenario that must NOT promote.
    /// </summary>
    public async Task<string> BuildForRankAsync(int rankDefinitionId, int thresholdDelta = 0)
    {
        _db.Database.EnsureCreated();
        await RankGateSeeder.SeedAsync(_db, NullLogger.Instance);

        var requirement = await _db.RankRequirements.AsNoTracking()
            .Where(r => r.RankDefinitionId == rankDefinitionId)
            .OrderBy(r => r.LevelNo)
            .FirstAsync();

        // ── Subject member ─────────────────────────────────────────────────────────
        var subjectId = NextId();
        AddMember(subjectId, sponsorId: null, memberType: MemberType.Ambassador);

        // ── Universal gate: sponsored members (at least 3, capping as needed) ─────
        // SponsoredMembers is no longer a per-rank axis; the universal gate requires >= 3.
        // sponsoredNeeded = Max(3, ExternalMembers) — 3 satisfies the gate; ExternalMembers
        // of them are MemberType.ExternalMember for the ExternalMembers per-rank axis.
        // Every sponsored member gets an Active membership worth 1 point (contributes to PCP).
        var sponsoredNeeded = Math.Max(3, requirement.ExternalMembers);
        for (var i = 0; i < sponsoredNeeded; i++)
        {
            var sponsoredId = NextId();
            var memberType = i < requirement.ExternalMembers ? MemberType.ExternalMember : MemberType.Ambassador;
            AddMember(sponsoredId, sponsorId: subjectId, memberType: memberType);
            AddActiveMembership(sponsoredId, points: 1);
        }

        // ── Enrollment tree: subject root ──────────────────────────────────────────
        _db.GenealogyTree.Add(Geno(subjectId, null, $"/{subjectId}/"));

        // Two direct ET-branch members (separate from the sponsored members above).
        // Branch A gets ceil(etTarget / 2.0) and Branch B gets floor(etTarget / 2.0) so
        // the two-branch raw sum equals etTarget EXACTLY (ceil + floor = target for any integer).
        // When the per-branch cap is binding the eligible (capped) sum still meets the threshold
        // for thresholdDelta = 0; when thresholdDelta = -1 the eligible sum is exactly target - 1
        // for EVERY rank, including even-threshold ones where ceil+ceil would silently swallow -1.
        var etTarget = Math.Max(0, requirement.EnrollmentTeam + thresholdDelta);
        if (etTarget > 0)
        {
            var branchA = (int)Math.Ceiling(etTarget / 2.0);
            var branchB = (int)Math.Floor(etTarget / 2.0);
            var branchPoints = new[] { branchA, branchB };
            for (var i = 0; i < 2; i++)
            {
                var branchId = NextId();
                // ET-branch members are genealogy children of the subject but NOT directly
                // sponsored by it — sponsorId is null so they don't inflate the gate's
                // sponsored count or the ExternalMembers axis.
                AddMember(branchId, sponsorId: null, memberType: MemberType.Ambassador);
                _db.GenealogyTree.Add(Geno(branchId, subjectId, $"/{subjectId}/{branchId}/"));
                _db.MemberStatistics.Add(Stat(branchId, enrollmentPoints: branchPoints[i]));
            }
        }

        // ── Dual team: left leg = ceil(dtTarget/2), right leg = floor(dtTarget/2) ─────
        // ceil + floor sums to dtTarget exactly; for thresholdDelta = -1 on even thresholds
        // this correctly yields dtTarget − 1 instead of silently rounding back up to dtTarget.
        var dtTarget = Math.Max(0, requirement.TeamPoints + thresholdDelta);
        _db.DualTeamTree.Add(new DualTeamEntity
        {
            MemberId = subjectId,
            HierarchyPath = $"/{subjectId}/",
            LeftLegPoints = (decimal)(int)Math.Ceiling(dtTarget / 2.0),
            RightLegPoints = (decimal)(int)Math.Floor(dtTarget / 2.0),
            CreatedBy = Tag, CreationDate = Now, LastUpdateDate = Now
        });

        // ── Subject's own membership: worth 9 PCP (gate requires ≥ 9 with 3 sponsored) ─
        AddActiveMembership(subjectId, points: 9);

        // ── Subject statistics: PersonalPoints satisfies the per-rank PersonalPoints axis ─
        // Defect-3 fix: use Math.Max(1, requirement.PersonalPoints).
        _db.MemberStatistics.Add(Stat(subjectId, enrollmentPoints: 0,
            personalPoints: Math.Max(1, requirement.PersonalPoints)));

        await _db.SaveChangesAsync();
        return subjectId;
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

    private string NextId() => $"{MemberPrefix}{_runId}-{++_seq:D4}";

    private void AddMember(string memberId, string? sponsorId, MemberType memberType) =>
        _db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = memberId, SponsorMemberId = sponsorId,
            FirstName = "Rank", LastName = "Validation", Email = $"{memberId}@rvh.test",
            MemberType = memberType, Country = "US", EnrollDate = Now,
            CreatedBy = Tag, CreationDate = Now, LastUpdateDate = Now
        });

    private void AddActiveMembership(string memberId, int points)
    {
        var orderId = $"ORD-{memberId}";
        _db.Orders.Add(new Orders
        {
            Id = orderId, MemberId = memberId, Status = OrderStatus.Completed, OrderDate = Now,
            TotalAmount = 0, CreatedBy = Tag, CreationDate = Now, LastUpdateDate = Now
        });
        var productId = $"PRD-{memberId}";
        _db.Products.Add(new Product
        {
            Id = productId, Name = "Membership", Description = "d", ImageUrl = "x",
            MonthlyFee = 0, SetupFee = 0, QualificationPoins = points,
            CreatedBy = Tag, CreationDate = Now, LastUpdateDate = Now
        });
        // Defect-1 fix: OrderDetail derives from AuditChangesLongKey — no LastUpdateDate.
        _db.OrderDetails.Add(new OrderDetail
        {
            OrderId = orderId, ProductId = productId, Quantity = 1, UnitPrice = 0,
            CreatedBy = Tag, CreationDate = Now
        });
        _db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = memberId, MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Now, LastOrderId = orderId,
            CreatedBy = Tag, CreationDate = Now, LastUpdateDate = Now
        });
    }

    private static GenealogyEntity Geno(string memberId, string? parentId, string path) => new()
    {
        MemberId = memberId, ParentMemberId = parentId, HierarchyPath = path,
        Level = path.Trim('/').Split('/').Length,
        CreatedBy = Tag, CreationDate = Now, LastUpdateDate = Now
    };

    private static MemberStatisticEntity Stat(string memberId, int enrollmentPoints, int personalPoints = 0) => new()
    {
        MemberId = memberId, EnrollmentPoints = enrollmentPoints, PersonalPoints = personalPoints,
        CreatedBy = Tag, CreationDate = Now
    };
}
