using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Jobs;

/// <summary>
/// HangFire recurring job — every 10 minutes.
/// Scans completed orders within the lookback window and creates missing sponsor
/// bonus earnings (Cat 1 Member Bonus + Cat 6/7 Builder Bonus) for any order
/// that qualified but was not paid — e.g. because FixedAmount was $0 at signup
/// time and admin configured it later, or due to a transient failure.
///
/// Selection logic mirrors SponsorBonusService.ComputeAsync:
///   Cat 1 — always paid (no rank gate).
///   Cat 6 — highest LifeTimeRank tier ≤ sponsor's current lifetime rank.
///   Cat 7 — same, separate category.
/// Skips types whose FixedAmount is still 0 (placeholder not yet configured).
/// </summary>
public class BuilderBonusSweepJob
{
    private const int LookbackDays = 3;
    private static readonly int[] EligibleLevelIds = [2, 3, 4]; // VIP=2, Elite=3, Turbo=4

    private readonly AppDbContext                    _db;
    private readonly IDateTimeProvider               _dateTime;
    private readonly ILogger<BuilderBonusSweepJob>  _logger;

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

        // Load all active sponsor bonus types that have a configured amount.
        // Ordered DESC by LifeTimeRank so MaxBy comparisons work correctly in memory.
        var allTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.IsSponsorBonus && (t.FixedAmount ?? 0m) > 0m)
            .OrderByDescending(t => t.LifeTimeRank)
            .ToListAsync(ct);

        if (allTypes.Count == 0)
        {
            _logger.LogWarning("Builder Bonus sweep: no sponsor bonus types with configured amounts — skipping.");
            return;
        }

        // ── Step 1: Resolve membership level per order (one query) ─────────────
        // Takes the highest eligible MembershipLevelId per order to avoid duplicate rows.
        var orderLevels = await (
            from od in _db.OrderDetails.AsNoTracking()
            join p  in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where p.MembershipLevelId.HasValue
               && EligibleLevelIds.Contains(p.MembershipLevelId!.Value)
            group p.MembershipLevelId!.Value by od.OrderId into g
            select new { OrderId = g.Key, LevelId = g.Max() }
        ).ToDictionaryAsync(x => x.OrderId, x => x.LevelId, ct);

        if (orderLevels.Count == 0) return;

        // ── Step 2: Completed orders within lookback, with a sponsor ────────────
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
                NewMemberId     = o.MemberId,
                SponsorMemberId = mp.SponsorMemberId!,
                OrderTotal      = o.TotalAmount
            }
        ).ToListAsync(ct);

        if (candidates.Count == 0) return;

        // ── Step 3: Sponsor lifetime ranks (batch) ───────────────────────────
        var sponsorIds = candidates.Select(c => c.SponsorMemberId).Distinct().ToList();
        var sponsorRanks = await _db.MemberRankHistories
            .AsNoTracking()
            .Where(r => sponsorIds.Contains(r.MemberId))
            .Join(_db.RankDefinitions.AsNoTracking(),
                  h => h.RankDefinitionId,
                  d => d.Id,
                  (h, d) => new { h.MemberId, d.SortOrder })
            .GroupBy(x => x.MemberId)
            .Select(g => new { MemberId = g.Key, MaxRank = g.Max(x => x.SortOrder) })
            .ToDictionaryAsync(x => x.MemberId, x => x.MaxRank, ct);

        // ── Step 4: Existing sponsor bonus earnings (batch) ──────────────────
        var orderIds          = candidates.Select(c => c.OrderId).Distinct().ToList();
        var sponsorTypeIds    = allTypes.Select(t => t.Id).ToList();

        var existingEarnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(e => orderIds.Contains(e.SourceOrderId)
                     && sponsorTypeIds.Contains(e.CommissionTypeId))
            .Select(e => new { e.SourceOrderId, e.CommissionTypeId, e.BeneficiaryMemberId })
            .ToListAsync(ct);

        // Key: "orderId|typeId|sponsorMemberId"
        var existingSet = existingEarnings
            .Select(e => EarningKey(e.SourceOrderId, e.CommissionTypeId, e.BeneficiaryMemberId))
            .ToHashSet();

        int awarded = 0;

        foreach (var candidate in candidates)
        {
            if (!orderLevels.TryGetValue(candidate.OrderId, out var membershipLevelId))
                continue;

            try
            {
                var added = ProcessOrder(
                    candidate.OrderId,
                    candidate.NewMemberId,
                    candidate.SponsorMemberId,
                    membershipLevelId,
                    candidate.OrderTotal,
                    allTypes,
                    sponsorRanks,
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
        string newMemberId,
        string sponsorMemberId,
        int membershipLevelId,
        decimal orderTotal,
        List<CommissionType> allTypes,
        Dictionary<string, int> sponsorRanks,
        HashSet<string> existingSet,
        DateTime now)
    {
        var sponsorLifetimeRank = sponsorRanks.GetValueOrDefault(sponsorMemberId, 0);

        // Mirrors SponsorBonusService: Turbo triggers Builder Bonus for Elite + Turbo levels.
        var builderLevels = membershipLevelId == 4
            ? new[] { 3, 4 }
            : new[] { membershipLevelId };

        // Cat 1 — once, for the actual product level only
        var memberBonus = allTypes.FirstOrDefault(
            t => t.CommissionCategoryId == 1 && t.LevelNo == membershipLevelId);

        // Cat 6 & 7 — highest qualifying tier per builder level
        var builderTypes = builderLevels
            .SelectMany(lvl => new CommissionType?[]
            {
                allTypes
                    .Where(t => t.CommissionCategoryId == 6
                             && t.LevelNo == lvl
                             && t.LifeTimeRank <= sponsorLifetimeRank)
                    .MaxBy(t => t.LifeTimeRank),
                allTypes
                    .Where(t => t.CommissionCategoryId == 7
                             && t.LevelNo == lvl
                             && t.LifeTimeRank <= sponsorLifetimeRank)
                    .MaxBy(t => t.LifeTimeRank)
            })
            .Where(t => t is not null)
            .Distinct();

        var typesToPay = new CommissionType?[] { memberBonus }
            .Concat(builderTypes)
            .Where(t => t is not null)
            .ToArray();
        int added = 0;

        foreach (var commType in typesToPay.Where(t => t is not null))
        {
            var amount = commType!.FixedAmount
                ?? Math.Round(orderTotal * commType.Percentage / 100m, 2);

            if (amount <= 0) continue;

            var key = EarningKey(orderId, commType.Id, sponsorMemberId);
            if (existingSet.Contains(key)) continue;

            _db.CommissionEarnings.Add(new CommissionEarning
            {
                BeneficiaryMemberId = sponsorMemberId,
                SourceMemberId      = newMemberId,
                SourceOrderId       = orderId,
                CommissionTypeId    = commType.Id,
                Amount              = amount,
                Status              = CommissionEarningStatus.Pending,
                EarnedDate          = now,
                PaymentDate         = now.AddDays(commType.PaymentDelayDays),
                PeriodDate          = now.Date,
                Notes               = $"Backfilled by builder-bonus-sweep on {now:yyyy-MM-dd HH:mm}",
                CreatedBy           = "builder-bonus-sweep",
                CreationDate        = now,
                LastUpdateDate      = now
            });

            existingSet.Add(key); // prevent re-addition in the same run
            added++;

            _logger.LogInformation(
                "Builder Bonus sweep: awarded Cat{Cat} (type {TypeId}) ${Amount} to {Sponsor} for order {Order}",
                commType.CommissionCategoryId, commType.Id, amount, sponsorMemberId, orderId);
        }

        return added;
    }

    private static string EarningKey(string orderId, int typeId, string memberId)
        => $"{orderId}|{typeId}|{memberId}";
}
