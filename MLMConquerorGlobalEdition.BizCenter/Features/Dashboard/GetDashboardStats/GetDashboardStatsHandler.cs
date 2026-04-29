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

        var w1NormalEnd = countdown.FastStartBonus1End;
        var fsb1Earned  = fsb1 != default && fsb1.EarnedDate <= w1NormalEnd;

        var w2Start = fsb1Earned ? fsb1.EarnedDate : countdown.FastStartBonus2Start;
        var w2End   = fsb1Earned ? fsb1.EarnedDate.AddDays(7) : countdown.FastStartBonus2End;
        var w3Start = fsb2 != default ? fsb2.EarnedDate : countdown.FastStartBonus3Start;
        var w3End   = fsb2 != default ? fsb2.EarnedDate.AddDays(7) : countdown.FastStartBonus3End;

        FsbWindowDto BuildWindow(int num, DateTime start, DateTime end, decimal amount)
        {
            var isCompleted    = amount > 0;
            var isExpired      = !isCompleted && now > end;
            var isActive       = !isCompleted && !isExpired && now >= start;
            var sponsoredCount = sponsoredEnrollments.Count(d => d >= start && d <= end);
            return new FsbWindowDto
            {
                WindowNumber   = num,
                StartDate      = start,
                EndDate        = end,
                Earned         = amount,
                IsCompleted    = isCompleted,
                IsActive       = isActive,
                SponsoredCount = sponsoredCount,
                Status         = isCompleted ? "Complete" : isActive ? "Active" : "Locked"
            };
        }

        return
        [
            BuildWindow(1, countdown.FastStartBonus1Start, w1NormalEnd, fsb1Earned ? fsb1.Amount : 0m),
            BuildWindow(2, w2Start, w2End, fsb2 != default ? fsb2.Amount : 0m),
            BuildWindow(3, w3Start, w3End, fsb3 != default ? fsb3.Amount : 0m),
        ];
    }
}
