using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public class SponsorBonusService : ISponsorBonusService
{
    private readonly AppDbContext _db;

    public SponsorBonusService(AppDbContext db) => _db = db;

    public async Task ComputeAsync(
        string? sponsorMemberId,
        string newMemberId,
        string orderId,
        decimal orderTotal,
        string createdBy,
        DateTime now,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sponsorMemberId)) return;

        var membershipLevelId = await (
            from od in _db.OrderDetails.AsNoTracking()
            join p  in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where od.OrderId == orderId && p.MembershipLevelId.HasValue
            select p.MembershipLevelId!.Value
        ).FirstOrDefaultAsync(ct);

        if (membershipLevelId <= 1) return;

        // Resolve active CorporatePromo rule for the product in this order.
        // Promo amounts are applied per commission category only when a matching
        // rule is active and the trigger flag for that category is enabled.
        var promoRule = await GetActivePromoRuleAsync(orderId, now, ct);

        var newMember = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == newMemberId)
            .Select(m => new { m.FirstName, m.LastName })
            .FirstOrDefaultAsync(ct);
        var memberFullName = newMember is not null
            ? $"{newMember.FirstName} {newMember.LastName}" : newMemberId;

        var orderNo = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => o.OrderNo ?? orderId)
            .FirstOrDefaultAsync(ct) ?? orderId;

        // Turbo signup fires Builder Bonus for both Elite (LevelNo=3) and Turbo (LevelNo=4).
        var builderLevels = membershipLevelId == 4
            ? new[] { 3, 4 }
            : new[] { membershipLevelId };

        var allTypes = await _db.CommissionTypes.AsNoTracking()
            .Where(t => t.IsActive && t.IsSponsorBonus && builderLevels.Contains(t.LevelNo))
            .OrderByDescending(t => t.LifeTimeRank)
            .ToListAsync(ct);

        bool promoForSponsorBonus  = promoRule is { TriggerSponsorBonus: true };
        bool promoForBuilderBonus  = promoRule is { TriggerBuilderBonus: true };
        bool promoForBuilderTurbo  = promoRule is { TriggerBuilderBonusTurbo: true };

        // ── Cat 1 — Member Bonus: direct sponsor only, one per builderLevel ────
        // Turbo fires two: Cat1/Elite ($40) + Cat1/Turbo ($40) = $80 total.
        // VIP and Elite fire one.
        foreach (var lvl in builderLevels)
        {
            var memberBonus = allTypes.FirstOrDefault(
                t => t.CommissionCategoryId == 1 && t.LevelNo == lvl);
            if (memberBonus is null) continue;

            var mbAmount = memberBonus.GetEffectiveAmount(promoForSponsorBonus)
                ?? Math.Round(orderTotal * memberBonus.Percentage / 100m, 2);
            if (mbAmount <= 0) continue;

            var exists = await _db.CommissionEarnings.AnyAsync(
                e => e.SourceOrderId       == orderId
                  && e.CommissionTypeId    == memberBonus.Id
                  && e.BeneficiaryMemberId == sponsorMemberId, ct);
            if (!exists)
                await _db.CommissionEarnings.AddAsync(
                    MakeEarning(sponsorMemberId, newMemberId, orderId, memberBonus,
                                mbAmount, now, orderNo, memberFullName, createdBy), ct);
        }

        // ── Cat 6 & 7 — differential Builder Bonus up the enrollment tree ───
        // Direct sponsor first, then uplines closest-first. Each member earns
        // (their full tier) minus (max tier already paid below them in the chain).
        if (!allTypes.Any(t => t.CommissionCategoryId is 6 or 7)) return;

        var directRank  = await GetLifetimeRankAsync(sponsorMemberId, ct);
        var uplineChain = await GetAncestorChainAsync(sponsorMemberId, ct);

        var chain = new List<(string MemberId, int LifetimeRank)>(uplineChain.Count + 1)
        {
            (sponsorMemberId, directRank)
        };
        chain.AddRange(uplineChain);

        // Tracks the highest tier amount committed so far per (category, level).
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

                    var promoActiveForCat = cat == 6 ? promoForBuilderBonus : promoForBuilderTurbo;
                    var tierAmount = bestType.GetEffectiveAmount(promoActiveForCat)
                        ?? Math.Round(orderTotal * bestType.Percentage / 100m, 2);
                    if (tierAmount <= 0) continue;

                    maxTierPaid.TryGetValue(dictKey, out var paidBelow);

                    // If an earning already exists for this member+order+type, advance
                    // the tracker so higher uplines are computed correctly, then skip.
                    var alreadyExists = await _db.CommissionEarnings.AnyAsync(
                        e => e.SourceOrderId      == orderId
                          && e.CommissionTypeId   == bestType.Id
                          && e.BeneficiaryMemberId == memberId, ct);
                    if (alreadyExists)
                    {
                        maxTierPaid[dictKey] = tierAmount;
                        continue;
                    }

                    var differential = tierAmount - paidBelow;
                    if (differential <= 0) continue;

                    await _db.CommissionEarnings.AddAsync(
                        MakeEarning(memberId, newMemberId, orderId, bestType,
                                    differential, now, orderNo, memberFullName, createdBy), ct);

                    maxTierPaid[dictKey] = tierAmount;
                }
            }
        }
    }

    public async Task TryReverseAsync(
        string cancelledMemberId,
        string signupOrderId,
        string? reason,
        DateTime now,
        string actorId,
        CancellationToken ct)
    {
        var sponsorBonusTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsSponsorBonus)
            .Select(t => new { t.Id, t.ReverseId, t.PaymentDelayDays })
            .ToListAsync(ct);

        if (sponsorBonusTypes.Count == 0) return;

        var typeIds = sponsorBonusTypes.Select(t => t.Id).ToList();

        var earnings = await _db.CommissionEarnings
            .Where(e => e.SourceOrderId  == signupOrderId
                     && e.SourceMemberId == cancelledMemberId
                     && typeIds.Contains(e.CommissionTypeId))
            .ToListAsync(ct);

        if (earnings.Count == 0) return;

        var reverseNote = string.IsNullOrWhiteSpace(reason)
            ? "Cancellation within 14-day window."
            : reason.Trim();

        foreach (var earning in earnings)
        {
            if (earning.Status == CommissionEarningStatus.Pending)
            {
                earning.Cancel(reverseNote);
                earning.LastUpdateBy = actorId;
                continue;
            }

            if (earning.Status != CommissionEarningStatus.Paid) continue;

            var originalType = sponsorBonusTypes.First(t => t.Id == earning.CommissionTypeId);
            if (originalType.ReverseId == 0) continue;

            var reverseType = await _db.CommissionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == originalType.ReverseId && t.IsActive, ct);
            if (reverseType is null) continue;

            var reversalExists = await _db.CommissionEarnings
                .AnyAsync(e => e.SourceOrderId    == signupOrderId
                            && e.SourceMemberId   == cancelledMemberId
                            && e.CommissionTypeId == originalType.ReverseId, ct);
            if (reversalExists) continue;

            await _db.CommissionEarnings.AddAsync(new CommissionEarning
            {
                BeneficiaryMemberId = earning.BeneficiaryMemberId,
                SourceMemberId      = cancelledMemberId,
                SourceOrderId       = signupOrderId,
                CommissionTypeId    = originalType.ReverseId,
                Amount              = -earning.Amount,
                Status              = CommissionEarningStatus.Pending,
                EarnedDate          = now,
                PaymentDate         = now.AddDays(reverseType.PaymentDelayDays),
                PeriodDate          = now.Date,
                Notes               = $"Reversal of earning {earning.Id} — {reverseNote}",
                CreatedBy           = actorId,
                CreationDate        = now,
                LastUpdateDate      = now
            }, ct);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first active <see cref="ProductCommissionPromo"/> that covers any product
    /// in <paramref name="orderId"/> under a currently-running CorporatePromo, or null if
    /// no promo is in effect. When non-null, the caller uses its trigger flags to decide
    /// whether AmountPromo applies per commission category.
    /// Uses a two-step query to avoid EF Core type-mismatch issues with the shadow FK
    /// on ProductCommissionPromo (long CorporatePromoId vs string CorporatePromo.Id).
    /// </summary>
    private async Task<ProductCommissionPromo?> GetActivePromoRuleAsync(
        string orderId, DateTime now, CancellationToken ct)
    {
        var activePromoIds = await _db.CorporatePromos
            .AsNoTracking()
            .Where(cp => cp.IsActive && cp.StartDate <= now && cp.EndDate >= now)
            .Select(cp => cp.Id)
            .ToListAsync(ct);

        if (activePromoIds.Count == 0) return null;

        return await (
            from od  in _db.OrderDetails.AsNoTracking()
            join pcp in _db.ProductCommissionPromos.AsNoTracking()
                on od.ProductId equals pcp.ProductId
            where od.OrderId == orderId
               && activePromoIds.Contains(EF.Property<string>(pcp, "CorporatePromoId1"))
            select pcp
        ).FirstOrDefaultAsync(ct);
    }

    private async Task<int> GetLifetimeRankAsync(string memberId, CancellationToken ct)
        => await _db.MemberRankHistories.AsNoTracking()
            .Where(r => r.MemberId == memberId)
            .Join(_db.RankDefinitions.AsNoTracking(),
                  h => h.RankDefinitionId, d => d.Id,
                  (_, d) => (int?)d.SortOrder)
            .MaxAsync(ct) ?? 0;

    /// <summary>
    /// Returns the enrollment-tree upline of <paramref name="memberId"/>,
    /// ordered closest-first, each paired with their lifetime rank.
    /// </summary>
    private async Task<List<(string MemberId, int LifetimeRank)>> GetAncestorChainAsync(
        string memberId, CancellationToken ct)
    {
        var path = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .Select(g => g.HierarchyPath)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(path)) return [];

        // "/root/a/b/sponsorId/" → segments ["root","a","b","sponsorId"]
        // Drop the last segment (member itself), reverse for closest-first.
        var segments    = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var ancestorIds = segments.SkipLast(1).Reverse().ToList();
        if (ancestorIds.Count == 0) return [];

        var ranks = await _db.MemberRankHistories.AsNoTracking()
            .Where(r => ancestorIds.Contains(r.MemberId))
            .Join(_db.RankDefinitions.AsNoTracking(),
                  h => h.RankDefinitionId, d => d.Id,
                  (h, d) => new { h.MemberId, d.SortOrder })
            .GroupBy(x => x.MemberId)
            .Select(g => new { MemberId = g.Key, MaxRank = g.Max(x => x.SortOrder) })
            .ToDictionaryAsync(x => x.MemberId, x => x.MaxRank, ct);

        return ancestorIds
            .Select(id => (id, ranks.GetValueOrDefault(id, 0)))
            .ToList();
    }

    private static CommissionEarning MakeEarning(
        string beneficiaryMemberId,
        string sourceMemberId,
        string sourceOrderId,
        CommissionType commType,
        decimal amount,
        DateTime now,
        string orderNo,
        string memberFullName,
        string createdBy)
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
            CreatedBy           = createdBy,
            CreationDate        = now,
            LastUpdateDate      = now
        };
}
