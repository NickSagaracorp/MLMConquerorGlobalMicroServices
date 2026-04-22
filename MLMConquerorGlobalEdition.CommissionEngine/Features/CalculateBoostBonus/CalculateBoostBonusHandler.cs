using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateBoostBonus;

public class CalculateBoostBonusHandler
    : IRequestHandler<CalculateBoostBonusCommand, Result<CalculationResultResponse>>
{
    // Only Elite (3) and Turbo (4) signups count toward Boost Bonus thresholds.
    private static readonly int[] EligibleMembershipLevels = [3, 4];

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculateBoostBonusHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculateBoostBonusCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // Comp plan: week runs Monday–Sunday
        var weekStart = command.PeriodDate.HasValue
            ? command.PeriodDate.Value.Date
            : now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(7);

        // Boost types: Gold ($600 / 6 members) and Platinum ($1,200 / 12 members).
        // Ordered ascending by LifeTimeRank so the Gold walk always happens before the Platinum walk.
        var boostTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && !t.IsPaidOnSignup && !t.ResidualBased
                     && !t.IsSponsorBonus && t.TriggerOrder > 0)
            .OrderBy(t => t.LifeTimeRank)
            .ThenBy(t => t.NewMembers)
            .ToListAsync(ct);

        if (boostTypes.Count == 0)
            return Result<CalculationResultResponse>.Failure(
                "NO_BOOST_TYPES", "No active Boost Bonus commission types configured.");

        var alreadyRan = await _db.CommissionEarnings
            .AnyAsync(e => boostTypes.Select(t => t.Id).Contains(e.CommissionTypeId)
                        && e.PeriodDate.HasValue && e.PeriodDate.Value.Date == weekStart, ct);
        if (alreadyRan)
            return Result<CalculationResultResponse>.Failure(
                "ALREADY_CALCULATED",
                $"Boost Bonus for week starting {weekStart:yyyy-MM-dd} was already calculated.");

        // ── Step 1: New Elite/Turbo members enrolled this week ─────────────────
        var eligibleNewMemberIds = await (
            from mp in _db.MemberProfiles.AsNoTracking()
            join o  in _db.Orders.AsNoTracking()       on mp.MemberId  equals o.MemberId
            join od in _db.OrderDetails.AsNoTracking() on o.Id         equals od.OrderId
            join p  in _db.Products.AsNoTracking()     on od.ProductId equals p.Id
            where mp.EnrollDate >= weekStart
               && mp.EnrollDate < weekEnd
               && o.Status == OrderStatus.Completed
               && p.MembershipLevelId.HasValue
               && EligibleMembershipLevels.Contains(p.MembershipLevelId!.Value)
            select mp.MemberId
        ).Distinct().ToListAsync(ct);

        if (eligibleNewMemberIds.Count == 0)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType          = "BoostBonus",
                RecordsCreated          = 0,
                TotalAmountCalculated   = 0,
                PeriodDate              = weekStart,
                SkippedReasons          = ["No Elite/Turbo enrollments this week."]
            });

        // ── Step 2: Enrollment tree paths for new members ──────────────────────
        var newMemberPaths = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => eligibleNewMemberIds.Contains(g.MemberId))
            .Select(g => new { g.MemberId, g.HierarchyPath })
            .ToDictionaryAsync(g => g.MemberId, g => g.HierarchyPath, ct);

        // ── Step 3: Lifetime rank SortOrder per ambassador ─────────────────────
        // Comp plan: eligibility uses lifetime rank (highest ever achieved), not current rank.
        var ambassadorLifetimeRanks = await _db.MemberRankHistories
            .AsNoTracking()
            .Join(_db.RankDefinitions.AsNoTracking(),
                  h => h.RankDefinitionId, d => d.Id,
                  (h, d) => new { h.MemberId, d.SortOrder })
            .GroupBy(x => x.MemberId)
            .Select(g => new { MemberId = g.Key, LifetimeRank = g.Max(x => x.SortOrder) })
            .ToDictionaryAsync(x => x.MemberId, x => x.LifetimeRank, ct);

        // ── Step 4: Build per-tier credit maps ─────────────────────────────────
        //
        // Walk rule per new member (single upward pass, stops when all tiers are filled):
        //
        //  1. Find the first ancestor with rank >= lowestTier.CurrentRank.
        //  2. Credit that ancestor for the HIGHEST tier their rank qualifies for.
        //     A Platinum-rank ancestor goes into the Platinum pool only — never Gold.
        //  3. If their rank only covers lower tier(s) (e.g., Gold but not Platinum),
        //     continue walking strictly ABOVE them to fill the remaining higher tier(s).
        //
        // This means a single enrollment can credit at most one person per tier, and
        // a higher-ranked ambassador is never double-credited for lower tiers.

        // commTypeId → { uplineId → [(newMemberId, legId)] }
        var tierCreditMaps = boostTypes.ToDictionary(
            t => t.Id,
            _ => new Dictionary<string, List<(string NewMemberId, string LegId)>>());

        var skipped = new List<string>();

        foreach (var newMemberId in eligibleNewMemberIds)
        {
            if (!newMemberPaths.TryGetValue(newMemberId, out var path) || string.IsNullOrEmpty(path))
            {
                skipped.Add($"{newMemberId}: no enrollment tree path found.");
                continue;
            }

            // segments = ["ROOT", "A", "B", ..., "newMemberId"]
            var segments    = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            int searchStart = segments.Length - 2; // direct parent of new member
            int tierIdx     = 0;                   // next unfilled tier index
            bool anyCredited = false;

            while (tierIdx < boostTypes.Count && searchStart >= 0)
            {
                var requiredTier = boostTypes[tierIdx]; // lowest unfilled tier
                string creditedUpline = string.Empty;
                string legId          = string.Empty;
                int    foundIndex     = -1;

                for (int i = searchStart; i >= 0; i--)
                {
                    var ancestorId = segments[i];
                    if (ambassadorLifetimeRanks.TryGetValue(ancestorId, out var rank)
                        && rank >= requiredTier.LifeTimeRank)
                    {
                        creditedUpline = ancestorId;
                        legId          = segments[i + 1];
                        foundIndex     = i;

                        // Find the HIGHEST tier this ancestor qualifies for.
                        // Tiers are ordered ascending — iterate forward from tierIdx.
                        int highestTierIdx = tierIdx;
                        for (int t = tierIdx + 1; t < boostTypes.Count; t++)
                        {
                            if (rank >= boostTypes[t].LifeTimeRank)
                                highestTierIdx = t;
                            else
                                break;
                        }

                        // Credit them for the highest applicable tier only.
                        var creditedTier = boostTypes[highestTierIdx];
                        if (!tierCreditMaps[creditedTier.Id].TryGetValue(creditedUpline, out var list))
                            tierCreditMaps[creditedTier.Id][creditedUpline] = list = [];
                        list.Add((NewMemberId: newMemberId, LegId: legId));

                        // Advance past all credited tiers and continue strictly ABOVE this ancestor.
                        tierIdx     = highestTierIdx + 1;
                        searchStart = foundIndex - 1;
                        anyCredited = true;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(creditedUpline))
                    break; // tree exhausted for this tier
            }

            if (!anyCredited)
                skipped.Add($"{newMemberId}: no Gold/Platinum rank upline found in enrollment tree.");
        }

        // ── Step 5: Qualify and create split earnings per tier independently ───
        //
        // Gold and Platinum are separate payout pools. An ambassador can earn both
        // in the same week if they accumulate enough credits in each pool.

        var earnings = new List<CommissionEarning>();

        foreach (var commType in boostTypes)
        {
            var creditMap = tierCreditMaps[commType.Id];

            foreach (var (uplineId, credits) in creditMap)
            {
                var capped = ApplyLegCap(credits, commType.NewMembers, commType.MaxEnrollmentTeamPointsPerBranch);

                if (commType.NewMembers > 0 && capped < commType.NewMembers)
                {
                    var breakdown = string.Join(", ",
                        credits.GroupBy(c => c.LegId).Select(g => $"leg {g.Key}={g.Count()}"));
                    skipped.Add(
                        $"{uplineId}: {credits.Count} credits but {commType.Name} threshold not met ({breakdown}).");
                    continue;
                }

                var totalAmount = commType.ActiveAmount
                    ?? Math.Round(capped * commType.Percentage / 100m, 2);
                if (totalAmount <= 0)
                {
                    skipped.Add($"{uplineId}: calculated amount is 0 for {commType.Name}.");
                    continue;
                }

                var half      = Math.Round(totalAmount / 2, 2);
                var remainder = totalAmount - half;

                earnings.Add(MakeEarning(uplineId, commType, half,      now, weekStart, BoostPortion.Qualification));
                earnings.Add(MakeEarning(uplineId, commType, remainder, now, weekStart, BoostPortion.Rebill));
            }
        }

        if (earnings.Count > 0)
        {
            await _db.CommissionEarnings.AddRangeAsync(earnings, ct);
            await _db.SaveChangesAsync(ct);
        }

        return Result<CalculationResultResponse>.Success(new CalculationResultResponse
        {
            CommissionType        = "BoostBonus",
            RecordsCreated        = earnings.Count,
            TotalAmountCalculated = earnings.Sum(e => e.Amount),
            PeriodDate            = weekStart,
            SkippedReasons        = skipped
        });
    }

    // Caps each enrollment-tree leg at floor(threshold * maxPerBranch) new members.
    // Gold  (threshold=6,  cap=0.5) → max 3 per leg.
    // Platinum (threshold=12, cap=0.5) → max 6 per leg.
    private static int ApplyLegCap(
        List<(string NewMemberId, string LegId)> credits,
        int threshold,
        double maxPerBranch)
    {
        if (threshold == 0) return credits.Count;
        var capPerLeg = (int)Math.Floor(threshold * maxPerBranch);
        return credits
            .GroupBy(c => c.LegId)
            .Sum(g => Math.Min(g.Count(), capPerLeg));
    }

    // Day 1–15  → 30th of the same month.
    // Day 16–31 → 15th of the following month.
    private static DateTime CalculatePaymentDate(DateTime earnedDate)
        => earnedDate.Day <= 15
            ? new DateTime(earnedDate.Year, earnedDate.Month, 30)
            : new DateTime(earnedDate.AddMonths(1).Year, earnedDate.AddMonths(1).Month, 15);

    private CommissionEarning MakeEarning(
        string beneficiaryId,
        CommissionType commType,
        decimal amount,
        DateTime now,
        DateTime periodDate,
        BoostPortion portion)
    {
        var payDate = portion == BoostPortion.Qualification
            ? CalculatePaymentDate(now)
            : CalculatePaymentDate(now).AddMonths(1);

        return new CommissionEarning
        {
            BeneficiaryMemberId = beneficiaryId,
            CommissionTypeId    = commType.Id,
            Amount              = amount,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = now,
            PaymentDate         = payDate,
            PeriodDate          = periodDate,
            Notes               = portion == BoostPortion.Qualification
                                    ? "boost-qualification-50pct"
                                    : "boost-rebill-50pct",
            CreatedBy           = _currentUser.UserId,
            CreationDate        = now,
            LastUpdateDate      = now
        };
    }

    private enum BoostPortion { Qualification, Rebill }
}
