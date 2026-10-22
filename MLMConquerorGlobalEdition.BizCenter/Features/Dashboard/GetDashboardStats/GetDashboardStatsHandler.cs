using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Dashboard;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Dashboard.GetDashboardStats;

public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStatsDto>>
{
    private const int FastStartBonusCategoryId = 2;

    private readonly AppDbContext            _db;
    private readonly ICurrentUserService     _currentUser;
    private readonly IDateTimeProvider       _dateTime;
    private readonly IRankComputationService _ranks;

    public GetDashboardStatsHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IRankComputationService ranks)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _ranks       = ranks;
    }

    public async Task<Result<DashboardStatsDto>> Handle(
        GetDashboardStatsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var now      = _dateTime.UtcNow;

        // Total earnings
        var totalEarnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.Status == CommissionEarningStatus.Paid)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        // Team size — direct children in enrollment tree
        var teamSize = await _db.GenealogyTree
            .AsNoTracking()
            .CountAsync(g => g.ParentMemberId == memberId, ct);

        // Token balance
        var tokenBalance = await _db.TokenBalances
            .AsNoTracking()
            .Where(tb => tb.MemberId == memberId)
            .SumAsync(tb => (int?)tb.Balance, ct) ?? 0;

        // Current rank — computed live by the shared service so this widget
        // always agrees with the residuals page and the admin profile views.
        var rankSummary = await _ranks.GetSummaryAsync(memberId, ct);
        var currentRank = rankSummary.CurrentRankName;

        // FSB windows — use countdown record + sponsored enrollments for proper states
        var fsbWindows = await BuildFsbWindowsAsync(memberId, now, ct);

        return Result<DashboardStatsDto>.Success(new DashboardStatsDto
        {
            TotalEarnings = totalEarnings,
            TeamSize      = teamSize,
            TokenBalance  = tokenBalance,
            CurrentRank   = currentRank,
            FsbWindows    = fsbWindows
        });
    }

    private async Task<List<FsbWindowDto>> BuildFsbWindowsAsync(
        string memberId, DateTime now, CancellationToken ct)
    {
        // Eligible sponsored member enrollments (Elite/Turbo active subscriptions)
        var eligibleMemberIds = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => s.SubscriptionStatus == MembershipStatus.Active)
            .Join(
                _db.MembershipLevels.Where(l => l.Name.Contains("Elite") || l.Name.Contains("Turbo")),
                s => s.MembershipLevelId, l => l.Id, (s, _) => s.MemberId)
            .ToHashSetAsync(ct);

        var sponsoredEnrollments = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == memberId && eligibleMemberIds.Contains(m.MemberId))
            .Select(m => m.EnrollDate)
            .ToListAsync(ct);

        // FSB earnings by TriggerOrder (non-cancelled)
        var fsbEarnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId && c.Status != CommissionEarningStatus.Cancelled)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == FastStartBonusCategoryId),
                c => c.CommissionTypeId, ct2 => ct2.Id,
                (c, ct2) => new { ct2.TriggerOrder, c.EarnedDate, c.Amount })
            .ToListAsync(ct);

        var earnByOrder = fsbEarnings
            .GroupBy(x => x.TriggerOrder)
            .ToDictionary(g => g.Key, g => (EarnedDate: g.Min(x => x.EarnedDate), Amount: g.Sum(x => x.Amount)));

        // Countdown record
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
        {
            return Enumerable.Range(1, 3)
                .Select(i => new FsbWindowDto { WindowNumber = i, Status = "Locked" })
                .ToList();
        }

        earnByOrder.TryGetValue(1, out var fsb1);
        earnByOrder.TryGetValue(2, out var fsb2);
        earnByOrder.TryGetValue(3, out var fsb3);

        // Comp-plan rule:
        //   • Days 0–7  → FSB1 normal window. Earn FSB1 here to unlock W2 + W3.
        //   • Days 7–14 → ONLY if FSB1 was NOT earned in the normal window,
        //                 the EXTENDED FSB1 window kicks in. The card stays
        //                 the same Window-1 card; its end-date stretches to
        //                 day 14. FSB2 and FSB3 stay locked forever in this
        //                 branch — the rule explicitly excludes them when
        //                 the normal FSB1 was missed.
        //   • If FSB1 IS earned in normal: W2 runs for 7 days from the FSB1
        //     earn date; if FSB2 is then earned, W3 runs for 7 days from the
        //     FSB2 earn date. Otherwise W3 stays locked.
        var fsb1EarnedInNormal = fsb1 != default && fsb1.EarnedDate <= countdown.FastStartBonus1End;
        var fsb1EarnedAnywhere = fsb1 != default;
        var fsb2EarnedAnywhere = fsb2 != default;

        // Window 1 — anchor on FSB1Start; end stretches to extended-end
        // when normal-window already passed without an earning.
        var w1Start = countdown.FastStartBonus1Start;
        var w1End   = fsb1EarnedInNormal
            ? countdown.FastStartBonus1End          // closed on time, end at day 7
            : countdown.FastStartBonus1ExtendedEnd; // not earned in normal → extended to day 14

        // Window 2 — gated on FSB1 having been earned in the NORMAL window.
        // Locked otherwise (regardless of how much time has passed).
        var w2Available = fsb1EarnedInNormal;
        var w2Start = w2Available ? fsb1.EarnedDate              : DateTime.MinValue;
        var w2End   = w2Available ? fsb1.EarnedDate.AddDays(7)   : DateTime.MinValue;

        // Window 3 — gated on Window 2 being available AND FSB2 having
        // been earned (regardless of when within W2).
        var w3Available = w2Available && fsb2EarnedAnywhere;
        var w3Start = w3Available ? fsb2.EarnedDate              : DateTime.MinValue;
        var w3End   = w3Available ? fsb2.EarnedDate.AddDays(7)   : DateTime.MinValue;

        FsbWindowDto BuildWindow(int num, DateTime start, DateTime end, decimal amount, bool locked = false)
        {
            var isCompleted    = amount > 0;
            var isExpired      = !isCompleted && !locked && now > end;
            var isActive       = !isCompleted && !isExpired && !locked && now >= start;
            // Skip sponsored-count math entirely on locked windows; the date
            // range is a sentinel and the count is meaningless there.
            var sponsoredCount = locked ? 0 : sponsoredEnrollments.Count(d => d >= start && d <= end);
            return new FsbWindowDto
            {
                WindowNumber   = num,
                StartDate      = locked ? null : start,
                EndDate        = locked ? null : end,
                Earned         = amount,
                IsCompleted    = isCompleted,
                IsActive       = isActive,
                SponsoredCount = sponsoredCount,
                Status         = locked      ? "Locked"
                               : isCompleted ? "Complete"
                               : isActive    ? "Active"
                                             : "Locked"
            };
        }

        return
        [
            BuildWindow(1, w1Start, w1End, fsb1EarnedAnywhere ? fsb1.Amount : 0m),
            BuildWindow(2, w2Start, w2End, fsb2EarnedAnywhere ? fsb2.Amount : 0m, locked: !w2Available),
            BuildWindow(3, w3Start, w3End, fsb3 != default     ? fsb3.Amount : 0m, locked: !w3Available),
        ];
    }
}
