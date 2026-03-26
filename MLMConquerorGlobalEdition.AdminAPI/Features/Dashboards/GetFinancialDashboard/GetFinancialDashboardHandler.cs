using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetFinancialDashboard;

public class GetFinancialDashboardHandler : IRequestHandler<GetFinancialDashboardQuery, Result<FinancialDashboardDto>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public GetFinancialDashboardHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db = db;
        _dateTime = dateTime;
    }

    public async Task<Result<FinancialDashboardDto>> Handle(
        GetFinancialDashboardQuery request, CancellationToken cancellationToken)
    {
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

        return Result<FinancialDashboardDto>.Success(dto);
    }
}
