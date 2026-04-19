using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
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

        // Resolve enrolled member's membership level (LevelNo: 2=VIP, 3=Elite, 4=Turbo).
        // Lifestyle Ambassador (<=1) carries no sponsor bonus.
        var membershipLevelId = await (
            from od in _db.OrderDetails.AsNoTracking()
            join p  in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where od.OrderId == orderId && p.MembershipLevelId.HasValue
            select p.MembershipLevelId!.Value
        ).FirstOrDefaultAsync(ct);

        if (membershipLevelId <= 1) return;

        // Resolve sponsor's lifetime rank (SortOrder). 0 = no rank history.
        var sponsorLifetimeRank = await _db.MemberRankHistories
            .AsNoTracking()
            .Where(r => r.MemberId == sponsorMemberId)
            .Join(_db.RankDefinitions.AsNoTracking(),
                  h => h.RankDefinitionId,
                  d => d.Id,
                  (_, d) => d.SortOrder)
            .DefaultIfEmpty(0)
            .MaxAsync(ct);

        // Turbo signups trigger Builder Bonus for both Elite (LevelNo=3) AND Turbo (LevelNo=4).
        // VIP/Elite signups only trigger their own level.
        var builderLevels = membershipLevelId == 4
            ? new[] { 3, 4 }
            : new[] { membershipLevelId };

        // Load all active sponsor bonus types for these levels in one query.
        var allTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.IsSponsorBonus && builderLevels.Contains(t.LevelNo))
            .OrderByDescending(t => t.LifeTimeRank)
            .ToListAsync(ct);

        // ── Cat 1 — Member Bonus ──────────────────────────────────────────────
        // Paid once, for the actual product level only (Turbo = $80, Elite = $40, VIP = $20).
        var memberBonus = allTypes.FirstOrDefault(
            t => t.CommissionCategoryId == 1 && t.LevelNo == membershipLevelId);

        // ── Cat 6 & 7 — Builder Bonus (per level in builderLevels) ───────────
        // For each level: pick the highest LifeTimeRank tier the sponsor qualifies for.
        // LifeTimeRank = 0 on the flat seed types means no minimum rank required.
        var builderTypes = builderLevels
            .SelectMany(lvl => new CommissionType?[]
            {
                // Cat 6 — highest qualifying tier for this level
                allTypes
                    .Where(t => t.CommissionCategoryId == 6
                             && t.LevelNo == lvl
                             && t.LifeTimeRank <= sponsorLifetimeRank)
                    .MaxBy(t => t.LifeTimeRank),

                // Cat 7 — highest qualifying tier for this level
                allTypes
                    .Where(t => t.CommissionCategoryId == 7
                             && t.LevelNo == lvl
                             && t.LifeTimeRank <= sponsorLifetimeRank)
                    .MaxBy(t => t.LifeTimeRank)
            })
            .Where(t => t is not null)
            .Distinct(); // guard against duplicate resolution across levels

        var typesToPay = new[] { memberBonus }
            .Concat(builderTypes)
            .Where(t => t is not null);

        foreach (var commType in typesToPay)
        {
            var amount = commType!.FixedAmount
                ?? Math.Round(orderTotal * commType.Percentage / 100m, 2);

            if (amount <= 0) continue; // placeholder not yet configured by admin

            var alreadyExists = await _db.CommissionEarnings
                .AnyAsync(e => e.SourceOrderId      == orderId
                            && e.CommissionTypeId    == commType.Id
                            && e.BeneficiaryMemberId == sponsorMemberId, ct);
            if (alreadyExists) continue;

            await _db.CommissionEarnings.AddAsync(new CommissionEarning
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
                CreatedBy           = createdBy,
                CreationDate        = now,
                LastUpdateDate      = now
            }, ct);
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
            .Where(e => e.SourceOrderId    == signupOrderId
                     && e.SourceMemberId   == cancelledMemberId
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
}
