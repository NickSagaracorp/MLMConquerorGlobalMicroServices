using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusSummary;

public class GetFastStartBonusSummaryHandler : IRequestHandler<GetFastStartBonusSummaryQuery, Result<CommissionBonusSummaryDto>>
{
    private const int FastStartBonusCategoryId = 2;

    private readonly AppDbContext                                                        _db;
    private readonly MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService  _currentUser;
    private readonly MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider    _dateTime;

    public GetFastStartBonusSummaryHandler(
        AppDbContext db,
        MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService currentUser,
        MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<CommissionBonusSummaryDto>> Handle(GetFastStartBonusSummaryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var now      = _dateTime.Now;

        // 1. Eligible sponsored member IDs (only active Elite / Turbo memberships count)
        var eligibleMemberIds = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => s.SubscriptionStatus == MembershipStatus.Active)
            .Join(
                _db.MembershipLevels.Where(l => l.Name.Contains("Elite") || l.Name.Contains("Turbo")),
                s => s.MembershipLevelId,
                l => l.Id,
                (s, _) => s.MemberId)
            .ToHashSetAsync(ct);

        // Enroll dates of directly-sponsored members with eligible membership
        var sponsoredEnrollments = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == memberId && eligibleMemberIds.Contains(m.MemberId))
            .Select(m => m.EnrollDate)
            .ToListAsync(ct);

        // 2. FSB earnings (all non-cancelled) — carry EarnedDate to compute dynamic windows
        var fsbEarnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId && c.Status != CommissionEarningStatus.Cancelled)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == FastStartBonusCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { ct2.TriggerOrder, c.EarnedDate, c.Amount })
            .ToListAsync(ct);

        var totalCount  = fsbEarnings.Count;
        var totalAmount = fsbEarnings.Sum(x => x.Amount);

        // Earliest earn date & total amount per TriggerOrder
        var earnByOrder = fsbEarnings
            .GroupBy(x => x.TriggerOrder)
            .ToDictionary(g => g.Key, g => (
                EarnedDate: g.Min(x => x.EarnedDate),
                Amount:     g.Sum(x => x.Amount)));

        // 3. Countdown record (keyed by UserId, not MemberId string)
        var memberUserId = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => m.UserId)
            .FirstOrDefaultAsync(ct);

        var countdown = memberUserId != default
            ? await _db.CommissionCountDowns
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MemberId == memberUserId, ct)
            : null;

        if (countdown is null)
            return Result<CommissionBonusSummaryDto>.Success(new CommissionBonusSummaryDto
            {
                Count       = totalCount,
                TotalAmount = totalAmount
            });

        // 4. FSB1 normal earned = TriggerOrder 1 commission earned before W1 normal end
        var w1NormalEnd = countdown.FastStartBonus1End;
        var w1ExtEnd    = countdown.FastStartBonus1ExtendedEnd;

        earnByOrder.TryGetValue(1, out var fsb1Earn);
        var fsb1NormalEarned   = fsb1Earn != default && fsb1Earn.EarnedDate <= w1NormalEnd;
        var fsb1ExtendedEarned = fsb1Earn != default && fsb1Earn.EarnedDate > w1NormalEnd && fsb1Earn.EarnedDate <= w1ExtEnd;

        earnByOrder.TryGetValue(2, out var fsb2Earn);
        earnByOrder.TryGetValue(3, out var fsb3Earn);

        // 5. Dynamic W2/W3 dates — FSB2 starts the moment FSB1 was earned (if earned early)
        var w2Start = fsb1NormalEarned
            ? fsb1Earn.EarnedDate
            : countdown.FastStartBonus2Start;
        var w2End   = fsb1NormalEarned
            ? fsb1Earn.EarnedDate.AddDays(7)
            : countdown.FastStartBonus2End;

        var w3Start = fsb2Earn != default
            ? fsb2Earn.EarnedDate
            : countdown.FastStartBonus3Start;
        var w3End   = fsb2Earn != default
            ? fsb2Earn.EarnedDate.AddDays(7)
            : countdown.FastStartBonus3End;

        // 6. Mode flags
        var isExtendedMode     = now > w1NormalEnd && !fsb1NormalEarned;
        var isDisqualifiedW2W3 = fsb1ExtendedEarned;

        // 7. Build windows
        FsbWindowDto BuildWindow(int num, bool isPromo, DateTime start, DateTime end, decimal amount, bool hidden)
        {
            var isCompleted    = amount > 0;  // earned = completed regardless of whether window has closed
            var isExpired      = !isCompleted && now > end;
            var isActive       = !isCompleted && !isExpired && now >= start;
            var sponsoredCount = sponsoredEnrollments.Count(d => d >= start && d <= end);
            return new FsbWindowDto
            {
                WindowNumber   = num,
                IsPromo        = isPromo,
                Amount         = amount,
                IsCompleted    = isCompleted,
                IsActive       = isActive,
                StartDate      = start,
                EndDate        = end,
                SponsoredCount = sponsoredCount,
                IsHidden       = hidden
            };
        }

        var w1NormalStart = countdown.FastStartBonus1Start;
        var w1ExtStart    = countdown.FastStartBonus1ExtendedStart;

        var windows = new List<FsbWindowDto>
        {
            // Normal W1
            BuildWindow(1, false, w1NormalStart, w1NormalEnd,
                fsb1NormalEarned ? fsb1Earn.Amount : 0m,
                hidden: false),

            // Normal W2 — hidden when in extended mode or disqualified
            BuildWindow(2, false, w2Start, w2End,
                fsb2Earn != default ? fsb2Earn.Amount : 0m,
                hidden: isExtendedMode || isDisqualifiedW2W3),

            // Normal W3 — hidden when in extended mode or disqualified
            BuildWindow(3, false, w3Start, w3End,
                fsb3Earn != default ? fsb3Earn.Amount : 0m,
                hidden: isExtendedMode || isDisqualifiedW2W3),

            // Extended W1 (Promo)
            BuildWindow(1, true, w1ExtStart, w1ExtEnd,
                fsb1ExtendedEarned ? fsb1Earn.Amount : 0m,
                hidden: false),
        };

        return Result<CommissionBonusSummaryDto>.Success(new CommissionBonusSummaryDto
        {
            Count              = totalCount,
            TotalAmount        = totalAmount,
            Windows            = windows,
            IsExtendedMode     = isExtendedMode,
            IsDisqualifiedW2W3 = isDisqualifiedW2W3
        });
    }
}
