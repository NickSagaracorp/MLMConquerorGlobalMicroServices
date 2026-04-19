using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsSummary;

public class GetCommissionsSummaryHandler : IRequestHandler<GetCommissionsSummaryQuery, Result<CommissionSummaryDto>>
{
    private readonly AppDbContext         _db;
    private readonly ICurrentUserService  _currentUser;
    private readonly IDateTimeProvider    _dateTime;

    public GetCommissionsSummaryHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<CommissionSummaryDto>> Handle(GetCommissionsSummaryQuery request, CancellationToken ct)
    {
        var memberId    = _currentUser.MemberId;
        var currentYear = _dateTime.UtcNow.Year;

        var earnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Select(c => new { c.Amount, c.Status, c.EarnedDate })
            .ToListAsync(ct);

        var dto = new CommissionSummaryDto
        {
            PendingTotal     = earnings.Where(c => c.Status == CommissionEarningStatus.Pending).Sum(c => c.Amount),
            PaidTotal        = earnings.Where(c => c.Status == CommissionEarningStatus.Paid).Sum(c => c.Amount),
            CurrentYearTotal = earnings.Where(c => c.Status == CommissionEarningStatus.Paid && c.EarnedDate.Year == currentYear).Sum(c => c.Amount)
        };

        return Result<CommissionSummaryDto>.Success(dto);
    }
}
