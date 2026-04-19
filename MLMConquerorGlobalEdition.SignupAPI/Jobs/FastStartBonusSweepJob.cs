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
                var earned = await ProcessSponsorAsync(countdown, sponsorMemberId, now, typeByTrigger, ct);
                if (earned)
                {
                    await _db.SaveChangesAsync(ct);
                    awarded++;
                }
                else if (_db.ChangeTracker.HasChanges())
                {
                    // FSB3 date repair — persist without a commission earning
                    await _db.SaveChangesAsync(ct);
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

    private async Task<bool> ProcessSponsorAsync(
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

        // Find the first open window that hasn't been earned yet
        var candidates = new (int TriggerOrder, DateTime Start, DateTime End)[]
        {
            (1, countdown.FastStartBonus1Start,         countdown.FastStartBonus1End),
            (1, countdown.FastStartBonus1ExtendedStart, countdown.FastStartBonus1ExtendedEnd),
            (2, countdown.FastStartBonus2Start,         countdown.FastStartBonus2End),
            (3, countdown.FastStartBonus3Start,         countdown.FastStartBonus3End),
        };

        int activeWindow     = 0;
        DateTime windowStart = default, windowEnd = default;
        CommissionType? commType = null;

        foreach (var c in candidates)
        {
            if (c.Start == default || now < c.Start || now > c.End) continue;
            if (!typeByTrigger.TryGetValue(c.TriggerOrder, out var cType)) continue;

            var alreadyEarned = await _db.CommissionEarnings
                .AnyAsync(e => e.BeneficiaryMemberId == sponsorMemberId
                            && e.CommissionTypeId == cType.Id
                            && e.Status != CommissionEarningStatus.Cancelled, ct);
            if (alreadyEarned) continue;

            activeWindow = c.TriggerOrder;
            windowStart  = c.Start;
            windowEnd    = c.End;
            commType     = cType;
            break;
        }

        if (activeWindow == 0 || commType is null) return false;

        // Check for at least 2 Elite/Turbo members enrolled within this window
        var eligible = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == sponsorMemberId
                     && m.EnrollDate >= windowStart
                     && m.EnrollDate <= windowEnd)
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

        if (eligible.Count < 2) return false;

        var m1 = eligible[0];
        var m2 = eligible[1];
        var notes = $"{m1.FirstName} {m1.LastName} ({m1.MemberId}) — {m2.FirstName} {m2.LastName} ({m2.MemberId})";

        var amount = (commType.FixedAmount ?? 0m) / 2m;
        if (amount <= 0) return false;

        var sourceOrderId = await _db.Orders
            .AsNoTracking()
            .Where(o => o.MemberId == m2.MemberId && o.Status == OrderStatus.Completed)
            .OrderByDescending(o => o.CreationDate)
            .Select(o => o.Id)
            .FirstOrDefaultAsync(ct)
            ?? $"SWEEP-{sponsorMemberId}-W{activeWindow}-{now:yyyyMMddHHmm}";

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

        // Open next window
        if (activeWindow == 1)
        {
            countdown.FastStartBonus2Start = now;
            countdown.FastStartBonus2End   = now.AddDays(7);
            countdown.LastUpdateDate       = now;
            countdown.LastUpdateBy         = "fsb-sweep";
        }
        else if (activeWindow == 2)
        {
            countdown.FastStartBonus3Start = now;
            countdown.FastStartBonus3End   = now.AddDays(7);
            countdown.LastUpdateDate       = now;
            countdown.LastUpdateBy         = "fsb-sweep";
        }

        _logger.LogInformation(
            "FSB sweep: awarded W{Window} ${Amount} to {Sponsor} — {Notes}",
            activeWindow, amount, sponsorMemberId, notes);

        return true;
    }
}
