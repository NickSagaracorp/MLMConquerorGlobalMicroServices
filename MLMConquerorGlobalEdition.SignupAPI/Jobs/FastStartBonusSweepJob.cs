using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Jobs;

/// <summary>
/// HangFire recurring job — every 5 minutes.
/// Scans all active FSB countdowns and awards FSB1/2/3 commissions to any sponsor
/// who has qualified (2 Elite/Turbo members in the window) but hasn't been paid yet.
/// Also repairs FSB3 window dates when they were never written (e.g. FSB2 was
/// backfilled against an old binary that lacked the date-advance code).
/// </summary>
public class FastStartBonusSweepJob
{
    private static readonly int[] EligibleLevelIds = [3, 4];

    private readonly AppDbContext       _db;
    private readonly IDateTimeProvider  _dateTime;
    private readonly ILogger<FastStartBonusSweepJob> _logger;

    public FastStartBonusSweepJob(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<FastStartBonusSweepJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = _dateTime.Now;

        // Pre-load FSB commission types — same for every sponsor, avoids N+1
        var fsbTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.IsPaidOnSignup && !t.IsSponsorBonus
                     && t.TriggerOrder >= 1 && t.TriggerOrder <= 3)
            .ToListAsync(ct);

        var typeByTrigger = fsbTypes.ToDictionary(t => t.TriggerOrder);

        if (typeByTrigger.Count == 0)
        {
            _logger.LogWarning("FSB sweep: no active FSB CommissionTypes found — skipping.");
            return;
        }

        // Load countdowns where at least one window could still be open,
        // or FSB3 dates haven't been set yet (needs derivation from FSB2 earning).
        var countdowns = await _db.CommissionCountDowns
            .Where(c => c.FastStartBonus1End  > now
                     || c.FastStartBonus2End  > now
                     || c.FastStartBonus3End  > now
                     || c.FastStartBonus3Start == default)
            .ToListAsync(ct);

        if (countdowns.Count == 0) return;

        // Resolve countdown.MemberId (UserId) → sponsor MemberId string in one query
        var userIds = countdowns.Select(c => c.MemberId).Distinct().ToList();
        var memberIdByUserId = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => userIds.Contains(m.UserId))
            .Select(m => new { m.UserId, m.MemberId })
            .ToDictionaryAsync(m => m.UserId, m => m.MemberId, ct);

        int awarded = 0;

        foreach (var countdown in countdowns)
        {
            if (!memberIdByUserId.TryGetValue(countdown.MemberId, out var sponsorMemberId))
                continue;

            try
            {
                var earnedCount = await ProcessSponsorAsync(countdown, sponsorMemberId, now, typeByTrigger, ct);
                if (_db.ChangeTracker.HasChanges())
                {
                    await _db.SaveChangesAsync(ct);
                    awarded += earnedCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FSB sweep: error processing sponsor {MemberId}", sponsorMemberId);
                _db.ChangeTracker.Clear();
            }
        }

        _logger.LogInformation("FSB sweep complete — {Count} commissions awarded.", awarded);
    }

    /// <summary>
    /// Processes ALL eligible FSB windows for a single sponsor in one sweep run.
    /// After firing W1 the countdown dates for W2 are written immediately, so W2
    /// can be evaluated in the same pass if it already has qualifying members.
    /// Returns the number of windows that earned a commission this run.
    /// </summary>
    private async Task<int> ProcessSponsorAsync(
        MemberCommissionCountDown countdown,
        string sponsorMemberId,
        DateTime now,
        Dictionary<int, CommissionType> typeByTrigger,
        CancellationToken ct)
    {
        // Repair FSB3 window dates if FSB2 was earned but FSB3 dates were never written
        if (countdown.FastStartBonus3Start == default && typeByTrigger.TryGetValue(2, out var fsb2Type))
        {
            var fsb2Earned = await _db.CommissionEarnings
                .AsNoTracking()
                .Where(e => e.BeneficiaryMemberId == sponsorMemberId
                         && e.CommissionTypeId == fsb2Type.Id
                         && e.Status != CommissionEarningStatus.Cancelled)
                .Select(e => (DateTime?)e.EarnedDate)
                .FirstOrDefaultAsync(ct);

            if (fsb2Earned.HasValue)
            {
                countdown.FastStartBonus3Start = fsb2Earned.Value;
                countdown.FastStartBonus3End   = fsb2Earned.Value.AddDays(7);
                countdown.LastUpdateDate       = now;
                countdown.LastUpdateBy         = "fsb-sweep";
            }
        }

        int earned = 0;

        // Track trigger orders awarded in this pass so the in-memory earning (not yet
        // saved) prevents the alternate W1 window slot from firing a duplicate earning
        // before SaveChangesAsync is called.
        var earnedThisPass = new HashSet<int>();

        // Process windows in order. After firing one window the countdown dates for the
        // next window are updated in memory, so the following iteration can immediately
        // check whether that window is now also eligible — covering all ready windows in
        // a single sweep run rather than requiring separate runs for each.
        var windowDefs = new (int TriggerOrder, Func<DateTime> GetStart, Func<DateTime> GetEnd)[]
        {
            (1, () => countdown.FastStartBonus1Start,         () => countdown.FastStartBonus1End),
            (1, () => countdown.FastStartBonus1ExtendedStart, () => countdown.FastStartBonus1ExtendedEnd),
            (2, () => countdown.FastStartBonus2Start,         () => countdown.FastStartBonus2End),
            (3, () => countdown.FastStartBonus3Start,         () => countdown.FastStartBonus3End),
        };

        foreach (var win in windowDefs)
        {
            var winStart = win.GetStart();
            var winEnd   = win.GetEnd();

            if (winStart == default || now < winStart || now > winEnd) continue;
            if (!typeByTrigger.TryGetValue(win.TriggerOrder, out var commType)) continue;

            // Skip if already earned in this pass (handles the dual W1 window slots) or in the DB.
            if (earnedThisPass.Contains(win.TriggerOrder)) continue;

            var alreadyEarned = await _db.CommissionEarnings
                .AnyAsync(e => e.BeneficiaryMemberId == sponsorMemberId
                            && e.CommissionTypeId == commType.Id
                            && e.Status != CommissionEarningStatus.Cancelled, ct);
            if (alreadyEarned) continue;

            // Check for at least 2 Elite/Turbo members enrolled within this window
            var eligible = await _db.MemberProfiles
                .AsNoTracking()
                .Where(m => m.SponsorMemberId == sponsorMemberId
                         && m.EnrollDate >= winStart
                         && m.EnrollDate <= winEnd)
                .Join(
                    _db.MembershipSubscriptions.AsNoTracking()
                        .Where(s => s.SubscriptionStatus == MembershipStatus.Active
                                 && EligibleLevelIds.Contains(s.MembershipLevelId)),
                    m => m.MemberId,
                    s => s.MemberId,
                    (m, _) => new { m.MemberId, m.FirstName, m.LastName, m.EnrollDate })
                .OrderBy(x => x.EnrollDate)
                .Take(2)
                .ToListAsync(ct);

            if (eligible.Count < 2) continue;

            var m1 = eligible[0];
            var m2 = eligible[1];
            var notes = $"{m1.FirstName} {m1.LastName} ({m1.MemberId}) — {m2.FirstName} {m2.LastName} ({m2.MemberId})";

            var amount = (commType.ActiveAmount ?? 0m) / 2m;
            if (amount <= 0) continue;

            var sourceOrderId = await _db.Orders
                .AsNoTracking()
                .Where(o => o.MemberId == m2.MemberId && o.Status == OrderStatus.Completed)
                .OrderByDescending(o => o.CreationDate)
                .Select(o => o.Id)
                .FirstOrDefaultAsync(ct)
                ?? $"SWEEP-{sponsorMemberId}-W{win.TriggerOrder}-{now:yyyyMMddHHmm}";

            await _db.CommissionEarnings.AddAsync(new CommissionEarning
            {
                BeneficiaryMemberId = sponsorMemberId,
                SourceMemberId      = m2.MemberId,
                SourceOrderId       = sourceOrderId,
                CommissionTypeId    = commType.Id,
                Amount              = amount,
                Status              = CommissionEarningStatus.Pending,
                EarnedDate          = now,
                PaymentDate         = now.AddDays(commType.PaymentDelayDays),
                PeriodDate          = now.Date,
                Notes               = notes,
                CreatedBy           = "fsb-sweep",
                CreationDate        = now,
                LastUpdateDate      = now
            }, ct);

            // Open the next window immediately so it can be evaluated in this same pass
            if (win.TriggerOrder == 1)
            {
                countdown.FastStartBonus2Start = now;
                countdown.FastStartBonus2End   = now.AddDays(7);
                countdown.LastUpdateDate       = now;
                countdown.LastUpdateBy         = "fsb-sweep";
            }
            else if (win.TriggerOrder == 2)
            {
                countdown.FastStartBonus3Start = now;
                countdown.FastStartBonus3End   = now.AddDays(7);
                countdown.LastUpdateDate       = now;
                countdown.LastUpdateBy         = "fsb-sweep";
            }

            earnedThisPass.Add(win.TriggerOrder);

            _logger.LogInformation(
                "FSB sweep: awarded W{Window} ${Amount} to {Sponsor} — {Notes}",
                win.TriggerOrder, amount, sponsorMemberId, notes);

            earned++;
        }

        return earned;
    }
}
