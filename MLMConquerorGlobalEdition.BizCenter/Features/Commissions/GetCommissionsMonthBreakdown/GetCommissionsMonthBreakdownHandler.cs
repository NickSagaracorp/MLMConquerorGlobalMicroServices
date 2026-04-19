using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsMonthBreakdown;

public class GetCommissionsMonthBreakdownHandler
    : IRequestHandler<GetCommissionsMonthBreakdownQuery, Result<List<CommissionMonthBreakdownDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetCommissionsMonthBreakdownHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<List<CommissionMonthBreakdownDto>>> Handle(
        GetCommissionsMonthBreakdownQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var raw = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.EarnedDate.Year  == request.Year
                     && c.EarnedDate.Month == request.Month)
            .Join(
                _db.CommissionTypes,
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new
                {
                    TypeName    = ct2.Name,
                    Description = ct2.Description ?? string.Empty,
                    c.EarnedDate,
                    c.PaymentDate,
                    c.Amount,
                    Status      = c.Status.ToString()
                })
            .OrderBy(x => x.TypeName)
            .ThenByDescending(x => x.EarnedDate)
            .ToListAsync(ct);

        var grouped = raw
            .GroupBy(x => x.TypeName)
            .Select(g => new CommissionMonthBreakdownDto
            {
                CommissionTypeName = g.Key,
                Items = g.Select(i => new CommissionMonthItemDto
                {
                    EarnedDate  = i.EarnedDate,
                    PaymentDate = i.PaymentDate,
                    Detail      = i.Description,
                    Amount      = i.Amount,
                    Status      = i.Status
                }).ToList()
            })
            .ToList();

        return Result<List<CommissionMonthBreakdownDto>>.Success(grouped);
    }
}
