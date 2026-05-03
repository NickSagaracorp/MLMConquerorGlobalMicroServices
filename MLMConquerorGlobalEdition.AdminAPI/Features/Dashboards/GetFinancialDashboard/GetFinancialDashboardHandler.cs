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
        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<FinancialDashboardDto>(CacheKeys.AdminFinancialDashboard, cancellationToken);
            if (cached is not null) return Result<FinancialDashboardDto>.Success(cached);
        }

        var now = _dateTime.Now;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var activeMembers = await _db.MemberProfiles
            .AsNoTracking()
            .CountAsync(m => m.Status == MemberAccountStatus.Active, cancellationToken);

        var commissionsPaid = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.Status == CommissionEarningStatus.Paid)
            .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0;

        var commissionsPending = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.Status == CommissionEarningStatus.Pending)
            .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0;

        var revenueThisMonth = await _db.Orders
            .AsNoTracking()
            .Where(o => o.OrderDate >= startOfMonth)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0;

        var dto = new FinancialDashboardDto
        {
            TotalMembersActive = activeMembers,
            TotalCommissionsPaid = commissionsPaid,
            TotalCommissionsPending = commissionsPending,
            TotalRevenueThisMonth = revenueThisMonth
        };

        await _cache.SetAsync(CacheKeys.AdminFinancialDashboard, dto, CacheKeys.AdminFinancialDashboardTtl, cancellationToken);
        return Result<FinancialDashboardDto>.Success(dto);
    }
}
