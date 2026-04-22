using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public class FastStartBonusService : IFastStartBonusService
{
    private static readonly int[] EligibleLevelIds = [3, 4]; // Elite=3, Turbo=4

    private readonly AppDbContext _db;

    public FastStartBonusService(AppDbContext db) => _db = db;

    public async Task ComputeAsync(
        string? sponsorMemberId,
        string newMemberId,
        string orderId,
        DateTime now,
        string createdBy,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sponsorMemberId)) return;

        // 1. Only Elite/Turbo products trigger FSB counting
        var membershipLevelId = await (
            from od in _db.OrderDetails.AsNoTracking()
            join p in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where od.OrderId == orderId && p.MembershipLevelId.HasValue
            select p.MembershipLevelId!.Value
        ).FirstOrDefaultAsync(ct);

        if (!EligibleLevelIds.Contains(membershipLevelId)) return;

        // 2. Resolve sponsor's ApplicationUser.Id (CountDown keyed by Guid UserId)
        var sponsorUserId = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == sponsorMemberId)
            .Select(m => m.UserId)
            .FirstOrDefaultAsync(ct);

        if (sponsorUserId == default) return;

        var countdown = await _db.CommissionCountDowns
            .FirstOrDefaultAsync(c => c.MemberId == sponsorUserId, ct);

        if (countdown is null) return;

        // If FSB3 dates were never written (e.g. FSB2 was backfilled against old code),
        // derive them from the FSB2 earning record so the window stays functional.
        if (countdown.FastStartBonus3Start == default)
        {
            var fsb2TypeId = await _db.CommissionTypes.AsNoTracking()
                .Where(t => t.IsActive && t.IsPaidOnSignup && !t.IsSponsorBonus && t.TriggerOrder == 2)
                .Select(t => t.Id)
                .FirstOrDefaultAsync(ct);

            if (fsb2TypeId > 0)
            {
                var fsb2Earned = await _db.CommissionEarnings.AsNoTracking()
                    .Where(e => e.BeneficiaryMemberId == sponsorMemberId
                             && e.CommissionTypeId == fsb2TypeId
                             && e.Status != CommissionEarningStatus.Cancelled)
                    .Select(e => (DateTime?)e.EarnedDate)
                    .FirstOrDefaultAsync(ct);

                if (fsb2Earned.HasValue)
                {
                    countdown.FastStartBonus3Start = fsb2Earned.Value;
                    countdown.FastStartBonus3End   = fsb2Earned.Value.AddDays(7);
                }
            }
        }

        // 3. Find the first window that is currently open AND not yet earned.
        // Windows are checked in priority order. W1 and W2 can overlap in time when FSB1
        // fires early and W2 is opened immediately — so we must skip already-earned windows
        // instead of relying on an if/else date chain.
        var windowCandidates = new (int TriggerOrder, DateTime Start, DateTime End)[]
        {
            (1, countdown.FastStartBonus1Start,         countdown.FastStartBonus1End),
            (1, countdown.FastStartBonus1ExtendedStart, countdown.FastStartBonus1ExtendedEnd),
            (2, countdown.FastStartBonus2Start,         countdown.FastStartBonus2End),
            (3, countdown.FastStartBonus3Start,         countdown.FastStartBonus3End),
        };

        int activeWindow = 0;
        DateTime windowStart = default, windowEnd = default;
        CommissionType? commType = null;

        foreach (var candidate in windowCandidates)
        {
            if (now < candidate.Start || now > candidate.End) continue;

            var candidateType = await _db.CommissionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsActive
                                       && t.IsPaidOnSignup
                                       && !t.IsSponsorBonus
                                       && t.TriggerOrder == candidate.TriggerOrder, ct);
            if (candidateType is null) continue;

            var alreadyEarned = await _db.CommissionEarnings
                .AnyAsync(e => e.BeneficiaryMemberId == sponsorMemberId
                            && e.CommissionTypeId == candidateType.Id
                            && e.Status != CommissionEarningStatus.Cancelled, ct);
            if (alreadyEarned) continue;

            activeWindow = candidate.TriggerOrder;
            windowStart  = candidate.Start;
            windowEnd    = candidate.End;
            commType     = candidateType;
            break;
        }

        if (activeWindow == 0 || commType is null) return;

        // 6. Get the committed Elite/Turbo sponsor already in this window (the first one)
        //    (current new member excluded — their subscription is in-memory Pending, not yet saved)
        var firstSponsor = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == sponsorMemberId
                     && m.MemberId != newMemberId
                     && m.EnrollDate >= windowStart && m.EnrollDate <= windowEnd)
            .Join(
                _db.MembershipSubscriptions.AsNoTracking()
                    .Where(s => s.SubscriptionStatus == MembershipStatus.Active
                             && EligibleLevelIds.Contains(s.MembershipLevelId)),
                m => m.MemberId,
                s => s.MemberId,
                (m, _) => new { m.MemberId, m.FirstName, m.LastName })
            .FirstOrDefaultAsync(ct);

        // The new member is the +1; FSB fires exactly when the 2nd qualifies
        if (firstSponsor is null) return; // committedCount != 1

        // 7. Build Notes with both member names for detail display
        var newMemberProfile = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == newMemberId)
            .Select(m => new { m.MemberId, m.FirstName, m.LastName })
            .FirstOrDefaultAsync(ct);

        var member1 = $"{firstSponsor.FirstName} {firstSponsor.LastName} ({firstSponsor.MemberId})";
        var member2 = newMemberProfile is not null
            ? $"{newMemberProfile.FirstName} {newMemberProfile.LastName} ({newMemberProfile.MemberId})"
            : newMemberId;
        var notes = $"{member1} — {member2}";

        // 8. Record the FSB earning — pay half now, remaining half fires on rebilling
        var amount = (commType.ActiveAmount ?? 0m) / 2m;
        if (amount <= 0) return;

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
            Notes               = notes,
            CreatedBy           = createdBy,
            CreationDate        = now,
            LastUpdateDate      = now
        }, ct);

        // 8. Advance the next window's dates from the moment FSB fires (dynamic start)
        if (activeWindow == 1)
        {
            countdown.FastStartBonus2Start = now;
            countdown.FastStartBonus2End   = now.AddDays(7);
            countdown.LastUpdateDate       = now;
            countdown.LastUpdateBy         = createdBy;
        }
        else if (activeWindow == 2)
        {
            countdown.FastStartBonus3Start = now;
            countdown.FastStartBonus3End   = now.AddDays(7);
            countdown.LastUpdateDate       = now;
            countdown.LastUpdateBy         = createdBy;
        }
    }
}
