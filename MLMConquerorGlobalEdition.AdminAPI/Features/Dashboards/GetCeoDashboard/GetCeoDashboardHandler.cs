using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetCeoDashboard;

public class GetCeoDashboardHandler : IRequestHandler<GetCeoDashboardQuery, Result<CeoDashboardDto>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public GetCeoDashboardHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db = db;
        _dateTime = dateTime;
    }

    public async Task<Result<CeoDashboardDto>> Handle(
        GetCeoDashboardQuery request, CancellationToken ct)
    {
        var now = _dateTime.Now;
        var today = now.Date;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfLastMonth = startOfMonth.AddMonths(-1);
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var last7Days = now.AddDays(-7);
        var next30Days = now.AddDays(30);

        var membersByStatus = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalMembers      = membersByStatus.Sum(g => g.Count);
        var activeMembers     = membersByStatus.FirstOrDefault(g => g.Status == MemberAccountStatus.Active)?.Count ?? 0;
        var inactiveMembers   = membersByStatus.FirstOrDefault(g => g.Status == MemberAccountStatus.Inactive)?.Count ?? 0;
        var suspendedMembers  = membersByStatus.FirstOrDefault(g => g.Status == MemberAccountStatus.Suspended)?.Count ?? 0;
        var terminatedMembers = membersByStatus.FirstOrDefault(g => g.Status == MemberAccountStatus.Terminated)?.Count ?? 0;

        var membersByType = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted)
            .GroupBy(m => m.MemberType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalAmbassadors     = membersByType.FirstOrDefault(g => g.Type == MemberType.Ambassador)?.Count ?? 0;
        var totalExternalMembers = membersByType.FirstOrDefault(g => g.Type == MemberType.ExternalMember)?.Count ?? 0;

        var newToday     = await _db.MemberProfiles.AsNoTracking().CountAsync(m => !m.IsDeleted && m.EnrollDate >= today, ct);
        var newThisWeek  = await _db.MemberProfiles.AsNoTracking().CountAsync(m => !m.IsDeleted && m.EnrollDate >= last7Days, ct);
        var newThisMonth = await _db.MemberProfiles.AsNoTracking().CountAsync(m => !m.IsDeleted && m.EnrollDate >= startOfMonth, ct);
        var newLastMonth = await _db.MemberProfiles.AsNoTracking().CountAsync(m => !m.IsDeleted && m.EnrollDate >= startOfLastMonth && m.EnrollDate < startOfMonth, ct);

        var revenueThisMonth = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderDate >= startOfMonth && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;

        var revenueLastMonth = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderDate >= startOfLastMonth && o.OrderDate < startOfMonth && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;

        var revenueThisYear = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderDate >= startOfYear && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;

        var ordersThisMonth = await _db.Orders.AsNoTracking()
            .CountAsync(o => o.OrderDate >= startOfMonth && o.Status != OrderStatus.Cancelled, ct);

        var avgOrderValue = ordersThisMonth > 0 ? Math.Round(revenueThisMonth / ordersThisMonth, 2) : 0;

        var commissionsPaidAll = await _db.CommissionEarnings.AsNoTracking()
            .Where(c => c.Status == CommissionEarningStatus.Paid)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0;

        var commissionsPaidThisMonth = await _db.CommissionEarnings.AsNoTracking()
            .Where(c => c.Status == CommissionEarningStatus.Paid && c.EarnedDate >= startOfMonth)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0;

        var commissionsPending = await _db.CommissionEarnings.AsNoTracking()
            .Where(c => c.Status == CommissionEarningStatus.Pending)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0;

        var activeSubs = await _db.MembershipSubscriptions.AsNoTracking()
            .CountAsync(s => s.SubscriptionStatus == MembershipStatus.Active, ct);

        var expiringSubs = await _db.MembershipSubscriptions.AsNoTracking()
            .CountAsync(s => s.SubscriptionStatus == MembershipStatus.Active
                          && s.RenewalDate.HasValue
                          && s.RenewalDate.Value >= now
                          && s.RenewalDate.Value <= next30Days, ct);

        var cancelledSubs = await _db.MembershipSubscriptions.AsNoTracking()
            .CountAsync(s => s.SubscriptionStatus == MembershipStatus.Cancelled
                          && s.CancellationDate.HasValue
                          && s.CancellationDate.Value >= startOfMonth, ct);

        var ticketsOpen       = await _db.Tickets.AsNoTracking().CountAsync(t => t.Status == TicketStatus.Open, ct);
        var ticketsInProgress = await _db.Tickets.AsNoTracking().CountAsync(t => t.Status == TicketStatus.InProgress, ct);
        var ticketsCritical   = await _db.Tickets.AsNoTracking().CountAsync(t => t.Priority == TicketPriority.Critical && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed, ct);
        var ticketsResolved   = await _db.Tickets.AsNoTracking().CountAsync(t => t.Status == TicketStatus.Resolved && t.CreationDate >= startOfMonth, ct);

        var tokenTotal     = await _db.TokenTransactions.AsNoTracking().CountAsync(t => t.ReferenceId != null, ct);
        var tokenUsed      = await _db.TokenTransactions.AsNoTracking().CountAsync(t => t.ReferenceId != null && t.UsedAt != null, ct);
        var tokenAvailable = tokenTotal - tokenUsed;

        var paymentsPending = await _db.PaymentHistories.AsNoTracking()
            .CountAsync(p => p.TransactionStatus == PaymentHistoryTransactionStatus.Pending, ct);
        var paymentsFailed  = await _db.PaymentHistories.AsNoTracking()
            .CountAsync(p => p.TransactionStatus == PaymentHistoryTransactionStatus.Failed, ct);

        var recentMembers = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted)
            .OrderByDescending(m => m.EnrollDate)
            .Take(10)
            .Select(m => new RecentMemberItem(
                m.MemberId,
                m.FirstName + " " + m.LastName,
                m.MemberType.ToString(),
                m.Status.ToString(),
                m.EnrollDate,
                m.SponsorMemberId))
            .ToListAsync(ct);

        // Determine last run date: most recent day with processed payments
        var lastProcessed = await _db.PaymentHistories
            .AsNoTracking()
            .Where(p => p.ProcessedAt.HasValue)
            .MaxAsync(p => (DateTime?)p.ProcessedAt, ct);

        var runDate      = lastProcessed?.Date ?? today;
        var runDateEnd   = runDate.AddDays(1);

        // Subscriptions due for auto-renewal on that date
        var dueRawList = await (
            from s in _db.MembershipSubscriptions.AsNoTracking()
            join ml in _db.MembershipLevels.AsNoTracking() on s.MembershipLevelId equals ml.Id
            where s.IsAutoRenew
               && s.RenewalDate.HasValue
               && s.RenewalDate.Value >= runDate
               && s.RenewalDate.Value < runDateEnd
            select new { s.Id, LevelName = ml.Name, ml.RenewalPrice }
        ).ToListAsync(ct);

        // Payment outcomes processed on that date (linked via Orders → Subscriptions)
        var paymentRawList = await (
            from ph in _db.PaymentHistories.AsNoTracking()
            join o  in _db.Orders.AsNoTracking() on ph.OrderId equals o.Id
            join s  in _db.MembershipSubscriptions.AsNoTracking()
                on o.MembershipSubscriptionId equals s.Id
            join ml in _db.MembershipLevels.AsNoTracking() on s.MembershipLevelId equals ml.Id
            where ph.ProcessedAt.HasValue
               && ph.ProcessedAt.Value >= runDate
               && ph.ProcessedAt.Value < runDateEnd
               && o.MembershipSubscriptionId != null
            select new
            {
                SubId       = s.Id,
                LevelName   = ml.Name,
                ml.RenewalPrice,
                ph.TransactionStatus,
                ph.Amount
            }
        ).ToListAsync(ct);

        // Build per-level stats in memory
        var dueByLevel     = dueRawList.GroupBy(x => new { x.LevelName, x.RenewalPrice }).ToList();
        var successByLevel = paymentRawList
            .Where(x => x.TransactionStatus == PaymentHistoryTransactionStatus.Captured)
            .GroupBy(x => x.LevelName).ToDictionary(g => g.Key, g => (Count: g.Count(), Revenue: g.Sum(x => x.Amount)));
        var failedByLevel  = paymentRawList
            .Where(x => x.TransactionStatus == PaymentHistoryTransactionStatus.Failed)
            .GroupBy(x => x.LevelName).ToDictionary(g => g.Key, g => (Count: g.Count(), Revenue: g.Sum(x => x.RenewalPrice)));

        var processedSubIds = paymentRawList.Select(x => x.SubId).ToHashSet();

        var billingByLevel = dueByLevel.Select(g =>
        {
            var lvl          = g.Key.LevelName;
            var price        = g.Key.RenewalPrice;
            var dueCount     = g.Count();
            var successCount = successByLevel.TryGetValue(lvl, out var s2) ? s2.Count : 0;
            var failedCount  = failedByLevel.TryGetValue(lvl, out var f2) ? f2.Count : 0;
            var pendingCount = g.Count(x => !processedSubIds.Contains(x.Id));
            var revCollected = successByLevel.TryGetValue(lvl, out var s3) ? s3.Revenue : 0;
            var revMissed    = failedByLevel.TryGetValue(lvl, out var f3) ? f3.Revenue : 0;
            return new BillingRunLevelItem(lvl, price, dueCount, successCount, failedCount, pendingCount, revCollected, revMissed);
        }).OrderByDescending(x => x.DueCount).ToList();

        var totalDue        = billingByLevel.Sum(x => x.DueCount);
        var totalSuccess    = billingByLevel.Sum(x => x.SuccessCount);
        var totalFailed     = billingByLevel.Sum(x => x.FailedCount);
        var totalPending    = billingByLevel.Sum(x => x.PendingCount);
        var totalCollected  = billingByLevel.Sum(x => x.RevenueCollected);
        var totalMissed     = billingByLevel.Sum(x => x.RevenueMissed);
        var successRate     = totalDue > 0 ? Math.Round(totalSuccess * 100.0m / totalDue, 1) : 0;

        var lastBillingRun = new BillingRunDto
        {
            RunDate          = runDate,
            TotalDue         = totalDue,
            TotalSuccessful  = totalSuccess,
            TotalFailed      = totalFailed,
            TotalPending     = totalPending,
            RevenueCollected = totalCollected,
            RevenueMissed    = totalMissed,
            SuccessRate      = successRate,
            ByLevel          = billingByLevel,
        };

        var ambassadorsByLevel = await (
            from m in _db.MemberProfiles.AsNoTracking()
            join s in _db.MembershipSubscriptions.AsNoTracking() on m.MemberId equals s.MemberId
            join ml in _db.MembershipLevels.AsNoTracking() on s.MembershipLevelId equals ml.Id
            where m.MemberType == MemberType.Ambassador
               && m.Status == MemberAccountStatus.Active
               && !m.IsDeleted
               && s.SubscriptionStatus == MembershipStatus.Active
            group m by new { ml.Name, ml.IsFree } into g
            orderby g.Count() descending
            select new AmbassadorsByLevelItem(g.Key.Name, g.Count(), g.Key.IsFree)
        ).ToListAsync(ct);

        var monthlySignups = await (
            from m in _db.MemberProfiles.AsNoTracking()
            join s in _db.MembershipSubscriptions.AsNoTracking() on m.MemberId equals s.MemberId
            join ml in _db.MembershipLevels.AsNoTracking() on s.MembershipLevelId equals ml.Id
            where m.EnrollDate >= startOfYear
               && !m.IsDeleted
               && s.SubscriptionStatus == MembershipStatus.Active
            group new { m, ml } by new
            {
                Month           = m.EnrollDate.Month,
                MembershipLevel = ml.Name,
                Status          = m.Status
            } into g
            select new MonthlySignupItem(
                g.Key.Month,
                g.Key.MembershipLevel,
                g.Key.Status.ToString(),
                g.Count())
        ).ToListAsync(ct);

        var membersByCountryRaw = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted
                     && m.Status == MemberAccountStatus.Active
                     && m.Country != null
                     && m.Country != string.Empty)
            .GroupBy(m => m.Country)
            .Select(g => new { CountryName = g.Key, ActiveCount = g.Count() })
            .OrderByDescending(x => x.ActiveCount)
            .ToListAsync(ct);

        var membersByCountry = membersByCountryRaw
            .Select(x => new MembersByCountryItem(x.CountryName!, x.ActiveCount))
            .ToList();

        var membershipBreakdown = await (
            from s in _db.MembershipSubscriptions.AsNoTracking()
            join ml in _db.MembershipLevels.AsNoTracking() on s.MembershipLevelId equals ml.Id
            where s.SubscriptionStatus == MembershipStatus.Active
            group s by new { ml.Name, ml.IsFree } into g
            orderby g.Count() descending
            select new MembershipBreakdownItem(g.Key.Name, g.Count(), g.Key.IsFree)
        ).ToListAsync(ct);

        var dto = new CeoDashboardDto
        {
            TotalMembers             = totalMembers,
            TotalAmbassadors         = totalAmbassadors,
            TotalExternalMembers     = totalExternalMembers,
            ActiveMembers            = activeMembers,
            InactiveMembers          = inactiveMembers,
            SuspendedMembers         = suspendedMembers,
            TerminatedMembers        = terminatedMembers,
            NewMembersToday          = newToday,
            NewMembersThisWeek       = newThisWeek,
            NewMembersThisMonth      = newThisMonth,
            NewMembersLastMonth      = newLastMonth,
            RevenueThisMonth         = revenueThisMonth,
            RevenueLastMonth         = revenueLastMonth,
            RevenueThisYear          = revenueThisYear,
            OrdersThisMonth          = ordersThisMonth,
            AvgOrderValueThisMonth   = avgOrderValue,
            CommissionsPaidAllTime   = commissionsPaidAll,
            CommissionsPaidThisMonth = commissionsPaidThisMonth,
            CommissionsPending       = commissionsPending,
            ActiveSubscriptions      = activeSubs,
            ExpiringSubscriptions30Days = expiringSubs,
            CancelledSubscriptionsThisMonth = cancelledSubs,
            TicketsOpen              = ticketsOpen,
            TicketsInProgress        = ticketsInProgress,
            TicketsCritical          = ticketsCritical,
            TicketsResolvedThisMonth = ticketsResolved,
            TokenCodesTotal          = tokenTotal,
            TokenCodesUsed           = tokenUsed,
            TokenCodesAvailable      = tokenAvailable,
            PaymentsPending          = paymentsPending,
            PaymentsFailed           = paymentsFailed,
            RecentMembers            = recentMembers,
            MembershipBreakdown      = membershipBreakdown,
            AmbassadorsByLevel       = ambassadorsByLevel,
            MonthlySignups           = monthlySignups,
            MembersByCountry         = membersByCountry,
            LastBillingRun           = lastBillingRun,
        };

        return Result<CeoDashboardDto>.Success(dto);
    }
}
