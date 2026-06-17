using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetFinancialDashboard;

/// <summary>
/// Financial executive dashboard. Aggregations across MemberProfiles +
/// CommissionEarnings + Orders. Cached globally for 3 minutes; admins click
/// "Refresh" with <c>?bypassCache=true</c> to force a recompute.
/// </summary>
public class GetFinancialDashboardHandler : IRequestHandler<GetFinancialDashboardQuery, Result<FinancialDashboardDto>>
{
    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICacheService     _cache;

    public GetFinancialDashboardHandler(AppDbContext db, IDateTimeProvider dateTime, ICacheService cache)
    {
        _db       = db;
        _dateTime = dateTime;
        _cache    = cache;
    }

    public async Task<Result<FinancialDashboardDto>> Handle(
        GetFinancialDashboardQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTime.Now;
        // Default window = current month (preserves prior behavior). Either bound may
        // be supplied independently; we clamp so 'to' is never before 'from'.
        var from = request.From ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = request.To   ?? now;
        if (to < from) to = from;

        var cacheKey = $"{CacheKeys.AdminFinancialDashboard}:{from:yyyyMMddHHmmss}:{to:yyyyMMddHHmmss}";

        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<FinancialDashboardDto>(cacheKey, cancellationToken);
            if (cached is not null) return Result<FinancialDashboardDto>.Success(cached);
        }

        // Active members is a point-in-time snapshot, not bound to the window.
        var activeMembers = await _db.MemberProfiles
            .AsNoTracking()
            .CountAsync(m => m.Status == MemberAccountStatus.Active, cancellationToken);

        var newMembersInRange = await _db.MemberProfiles
            .AsNoTracking()
            .CountAsync(m => m.EnrollDate >= from && m.EnrollDate <= to, cancellationToken);

        // Commissions are bucketed by EarnedDate (when the liability was incurred) so
        // they line up with revenue's OrderDate on the same time axis.
        var commissionsPaid = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.Status == CommissionEarningStatus.Paid && c.EarnedDate >= from && c.EarnedDate <= to)
            .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0;

        var commissionsPending = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.Status == CommissionEarningStatus.Pending && c.EarnedDate >= from && c.EarnedDate <= to)
            .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0;

        var revenue = await _db.Orders
            .AsNoTracking()
            .Where(o => o.OrderDate >= from && o.OrderDate <= to)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0;

        var totalCommissions = commissionsPaid + commissionsPending;
        var dto = new FinancialDashboardDto
        {
            RangeFrom               = from,
            RangeTo                 = to,
            TotalMembersActive      = activeMembers,
            NewMembersInRange       = newMembersInRange,
            TotalRevenue            = revenue,
            TotalCommissionsPaid    = commissionsPaid,
            TotalCommissionsPending = commissionsPending,
            NetCashFlow             = revenue - totalCommissions,
            CommissionToRevenuePct  = revenue > 0 ? Math.Round(totalCommissions / revenue * 100m, 2) : 0
        };

        await _cache.SetAsync(cacheKey, dto, CacheKeys.AdminFinancialDashboardTtl, cancellationToken);
        return Result<FinancialDashboardDto>.Success(dto);
    }
}
