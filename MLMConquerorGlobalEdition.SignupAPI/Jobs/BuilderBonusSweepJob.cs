using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Jobs;

/// <summary>
/// HangFire recurring job — every 10 minutes.
/// Scans completed orders within the lookback window and backfills any missing
/// sponsor bonus earnings using the same differential model as SponsorBonusService:
///
///   Cat 1 — Member Bonus paid once to the direct sponsor only.
///   Cat 6 / 7 — each upline in the enrollment tree earns (their tier amount)
///                minus (the highest tier already committed below them in the chain).
///                Total paid across the chain equals the highest-ranked member's
///                full tier.  Members whose rank is lower than someone below them
///                receive nothing.
/// </summary>
public class BuilderBonusSweepJob
{
    private const int LookbackDays = 7;
    private static readonly int[] EligibleLevelIds = [2, 3, 4]; // VIP=2, Elite=3, Turbo=4

    private readonly AppDbContext                   _db;
    private readonly IDateTimeProvider              _dateTime;
    private readonly ILogger<BuilderBonusSweepJob> _logger;

    public BuilderBonusSweepJob(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<BuilderBonusSweepJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now   = _dateTime.Now;
        var since = now.AddDays(-LookbackDays);

        // All active sponsor bonus types with a configured amount, ordered DESC so
        // MaxBy comparisons work correctly when selecting highest qualifying tier.
        var allTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.IsSponsorBonus && (t.Amount ?? 0m) > 0m)
            .OrderByDescending(t => t.LifeTimeRank)
            .ToListAsync(ct);

        if (allTypes.Count == 0)
        {
            _logger.LogWarning("Builder Bonus sweep: no sponsor bonus types with configured amounts — skipping.");
            return;
        }

        // ── Step 1: Membership level per order ───────────────────────────────
        var orderLevels = await (
            from od in _db.OrderDetails.AsNoTracking()
            join p  in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where p.MembershipLevelId.HasValue
               && EligibleLevelIds.Contains(p.MembershipLevelId!.Value)
            group p.MembershipLevelId!.Value by od.OrderId into g
            select new { OrderId = g.Key, LevelId = g.Max() }
        ).ToDictionaryAsync(x => x.OrderId, x => x.LevelId, ct);

        if (orderLevels.Count == 0) return;

        // ── Step 2: Completed orders within lookback, with a sponsor ─────────
        var candidates = await (
            from o  in _db.Orders.AsNoTracking()
            join mp in _db.MemberProfiles.AsNoTracking() on o.MemberId equals mp.MemberId
            where o.Status == OrderStatus.Completed
               && o.CreationDate >= since
               && mp.SponsorMemberId != null
               && orderLevels.Keys.Contains(o.Id)
            select new
            {
                OrderId         = o.Id,
                OrderNo         = o.OrderNo ?? o.Id,
                NewMemberId     = o.MemberId,
                SponsorMemberId = mp.SponsorMemberId!,
                OrderTotal      = o.TotalAmount
            }
        ).ToListAsync(ct);

        if (candidates.Count == 0) return;

        // ── Step 3: New member full names ────────────────────────────────────
        var newMemberIds = candidates.Select(c => c.NewMemberId).Distinct().ToList();
        var memberNames = await _db.MemberProfiles.AsNoTracking()
            .Where(m => newMemberIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, m.FirstName, m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => $"{m.FirstName} {m.LastName}", ct);

        // ── Step 4: Genealogy paths → ancestor chains for all sponsors ───────
        var sponsorIds = candidates.Select(c => c.SponsorMemberId).Distinct().ToList();

        var genealogyPaths = await _db.GenealogyTree.AsNoTracking()
            .Where(g => sponsorIds.Contains(g.MemberId))
            .Select(g => new { g.MemberId, g.HierarchyPath })
            .ToDictionaryAsync(g => g.MemberId, g => g.HierarchyPath, ct);

        // Parse each sponsor's path; collect all unique member IDs across every chain.
        var allChainMemberIds  = new HashSet<string>(sponsorIds);
        var sponsorAncestors   = new Dictionary<string, List<string>>(); // sponsorId → [closest, …, root]

        foreach (var (sponsorId, path) in genealogyPaths)
        {
            if (string.IsNullOrEmpty(path))
            {
                sponsorAncestors[sponsorId] = [];
                continue;
            }

            // "/root/a/b/sponsorId/" → drop last segment (self), reverse for closest-first
            var segments  = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var ancestors = segments.SkipLast(1).Reverse().ToList();
            sponsorAncestors[sponsorId] = ancestors;
            foreach (var a in ancestors)
                allChainMemberIds.Add(a);
        }

        // ── Step 5: Lifetime ranks for every member in every chain (batch) ───
        var allChainMemberIdsList = allChainMemberIds.ToList();
        var allChainRanks = await _db.MemberRankHistories.AsNoTracking()
            .Where(r => allChainMemberIdsList.Contains(r.MemberId))
            .Join(_db.RankDefinitions.AsNoTracking(),
                  h => h.RankDefinitionId, d => d.Id,
                  (h, d) => new { h.MemberId, d.SortOrder })
            .GroupBy(x => x.MemberId)
            .Select(g => new { MemberId = g.Key, MaxRank = g.Max(x => x.SortOrder) })
            .ToDictionaryAsync(x => x.MemberId, x => x.MaxRank, ct);

        // ── Step 6: Existing sponsor bonus earnings for all orders + chain ───
        var orderIds       = candidates.Select(c => c.OrderId).Distinct().ToList();
        var sponsorTypeIds = allTypes.Select(t => t.Id).ToList();

        var existingEarnings = await _db.CommissionEarnings.AsNoTracking()
            .Where(e => e.BeneficiaryMemberId != null
                     && orderIds.Contains(e.SourceOrderId!)
                     && sponsorTypeIds.Contains(e.CommissionTypeId)
                     && allChainMemberIdsList.Contains(e.BeneficiaryMemberId!))
            .Select(e => new { e.SourceOrderId, e.CommissionTypeId, e.BeneficiaryMemberId })
            .ToListAsync(ct);

        // Key: "orderId|typeId|memberId" — used to skip already-paid slots and for
        // intra-run idempotency so the same earning is never added twice.
        var existingSet = existingEarnings
            .Where(e => e.SourceOrderId != null && e.BeneficiaryMemberId != null)
            .Select(e => EarningKey(e.SourceOrderId!, e.CommissionTypeId, e.BeneficiaryMemberId!))
            .ToHashSet();

        int awarded = 0;

        foreach (var candidate in candidates)
        {
            if (!orderLevels.TryGetValue(candidate.OrderId, out var membershipLevelId))
                continue;

            try
            {
                var memberFullName = memberNames.GetValueOrDefault(
                    candidate.NewMemberId, candidate.NewMemberId);

                var ancestors = sponsorAncestors.GetValueOrDefault(
                    candidate.SponsorMemberId, []);

                var added = ProcessOrder(
                    candidate.OrderId,
                    candidate.OrderNo,
                    candidate.NewMemberId,
                    memberFullName,
                    candidate.SponsorMemberId,
                    membershipLevelId,
                    candidate.OrderTotal,
                    allTypes,
                    allChainRanks,
                    ancestors,
                    existingSet,
                    now);

                if (added > 0)
                {
                    await _db.SaveChangesAsync(ct);
                    awarded += added;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Builder Bonus sweep: error processing order {OrderId}", candidate.OrderId);
                _db.ChangeTracker.Clear();
            }
        }

        _logger.LogInformation("Builder Bonus sweep complete — {Count} commissions awarded.", awarded);
    }

    private int ProcessOrder(
        string orderId,
        string orderNo,
        string newMemberId,
        string memberFullName,
        string sponsorMemberId,
        int membershipLevelId,
        decimal orderTotal,
        List<CommissionType> allTypes,
        Dictionary<string, int> allChainRanks,
        List<string> sponsorAncestors,
        HashSet<string> existingSet,
        DateTime now)
    {
        int added = 0;

        // ── Cat 1 — Member Bonus: direct sponsor only, one per builderLevel ────
        // Turbo fires two: Cat1/Elite ($40) + Cat1/Turbo ($40) = $80 total.
        var builderLevels = membershipLevelId == 4
            ? new[] { 3, 4 }
            : new[] { membershipLevelId };

        foreach (var lvl in builderLevels)
        {
            var memberBonus = allTypes.FirstOrDefault(
                t => t.CommissionCategoryId == 1 && t.LevelNo == lvl);
            if (memberBonus is null) continue;

            var mbAmount = memberBonus.ActiveAmount
                ?? Math.Round(orderTotal * memberBonus.Percentage / 100m, 2);
            if (mbAmount <= 0) continue;

            var mbKey = EarningKey(orderId, memberBonus.Id, sponsorMemberId);
            if (!existingSet.Contains(mbKey))
            {
                _db.CommissionEarnings.Add(MakeEarning(
                    sponsorMemberId, newMemberId, orderId, memberBonus,
                    mbAmount, now, orderNo, memberFullName));
                existingSet.Add(mbKey);
                added++;
                _logger.LogInformation(
                    "Builder Bonus sweep: awarded Cat1 (type {TypeId}) ${Amount} to {Sponsor} for order {Order}",
                    memberBonus.Id, mbAmount, sponsorMemberId, orderId);
            }
        }

        // Full chain: direct sponsor first, then ancestors closest-first.
        var chain = new List<(string MemberId, int Rank)>(sponsorAncestors.Count + 1)
        {
            (sponsorMemberId, allChainRanks.GetValueOrDefault(sponsorMemberId, 0))
        };
        foreach (var ancestorId in sponsorAncestors)
            chain.Add((ancestorId, allChainRanks.GetValueOrDefault(ancestorId, 0)));

        // maxTierPaid[(cat, lvl)] = highest tier amount committed to anyone below
        var maxTierPaid = new Dictionary<(int cat, int lvl), decimal>();

        foreach (var (memberId, lifetimeRank) in chain)
        {
            foreach (var lvl in builderLevels)
            {
                foreach (var cat in new[] { 6, 7 })
                {
                    var dictKey  = (cat, lvl);
                    var bestType = allTypes
                        .Where(t => t.CommissionCategoryId == cat
                                 && t.LevelNo             == lvl
                                 && t.LifeTimeRank        <= lifetimeRank)
                        .MaxBy(t => t.LifeTimeRank);

                    if (bestType is null) continue;

                    var tierAmount = bestType.ActiveAmount
                        ?? Math.Round(orderTotal * bestType.Percentage / 100m, 2);
                    if (tierAmount <= 0) continue;

                    maxTierPaid.TryGetValue(dictKey, out var paidBelow);

                    var earningKey = EarningKey(orderId, bestType.Id, memberId);
                    if (existingSet.Contains(earningKey))
                    {
                        // Already paid — advance tracker so higher uplines compute correctly.
                        maxTierPaid[dictKey] = tierAmount;
                        continue;
                    }

                    var differential = tierAmount - paidBelow;
                    if (differential <= 0) continue;

                    _db.CommissionEarnings.Add(MakeEarning(
                        memberId, newMemberId, orderId, bestType,
                        differential, now, orderNo, memberFullName));

                    existingSet.Add(earningKey);
                    maxTierPaid[dictKey] = tierAmount;
                    added++;

                    _logger.LogInformation(
                        "Builder Bonus sweep: awarded Cat{Cat} (type {TypeId}) ${Amount} to {Member} for order {Order}",
                        cat, bestType.Id, differential, memberId, orderId);
                }
            }
        }

        return added;
    }

    private static CommissionEarning MakeEarning(
        string beneficiaryMemberId,
        string sourceMemberId,
        string sourceOrderId,
        CommissionType commType,
        decimal amount,
        DateTime now,
        string orderNo,
        string memberFullName)
        => new()
        {
            BeneficiaryMemberId = beneficiaryMemberId,
            SourceMemberId      = sourceMemberId,
            SourceOrderId       = sourceOrderId,
            CommissionTypeId    = commType.Id,
            Amount              = amount,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = now,
            PaymentDate         = now.AddDays(commType.PaymentDelayDays),
            PeriodDate          = now.Date,
            Notes               = $"Order {orderNo} — {memberFullName} ({sourceMemberId})",
            CreatedBy           = $"builder-bonus-sweep · {now:yyyy-MM-dd HH:mm}",
            CreationDate        = now,
            LastUpdateDate      = now
        };

    private static string EarningKey(string orderId, int typeId, string memberId)
        => $"{orderId}|{typeId}|{memberId}";
}
