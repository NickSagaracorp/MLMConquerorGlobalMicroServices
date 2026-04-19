using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsHistory;

public class GetCommissionsHistoryHandler : IRequestHandler<GetCommissionsHistoryQuery, Result<List<CommissionHistoryYearDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetCommissionsHistoryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<List<CommissionHistoryYearDto>>> Handle(GetCommissionsHistoryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Group by year and month in the database, then project into DTOs
        var grouped = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId && c.Status == CommissionEarningStatus.Paid)
            .GroupBy(c => new { c.EarnedDate.Year, c.EarnedDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Sum(c => c.Amount)
            })
            .OrderByDescending(g => g.Year)
            .ThenByDescending(g => g.Month)
            .ToListAsync(ct);

        var years = grouped
            .GroupBy(g => g.Year)
            .Select(yg => new CommissionHistoryYearDto
            {
                Year        = yg.Key,
                TotalIncome = yg.Sum(m => m.Total),
                Months      = yg
                    .OrderByDescending(m => m.Month)
                    .Select(m => new CommissionHistoryMonthDto
                    {
                        MonthNo     = m.Month,
                        MonthName   = new DateTime(m.Year, m.Month, 1).ToString("MMMM"),
                        TotalIncome = m.Total
                    })
                    .ToList()
            })
            .ToList();

        return Result<List<CommissionHistoryYearDto>>.Success(years);
    }
}
