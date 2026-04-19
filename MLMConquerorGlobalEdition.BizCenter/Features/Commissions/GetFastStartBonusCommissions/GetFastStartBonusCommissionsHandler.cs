using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusCommissions;

public class GetFastStartBonusCommissionsHandler : IRequestHandler<GetFastStartBonusCommissionsQuery, Result<PagedResult<CommissionEarningDto>>>
{
    private const int FastStartBonusCategoryId = 2;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetFastStartBonusCommissionsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<CommissionEarningDto>>> Handle(GetFastStartBonusCommissionsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var query = _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == FastStartBonusCategoryId),
                c => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { Earning = c, CommType = ct2 })
            .Join(
                _db.CommissionCategories,
                x => x.CommType.CommissionCategoryId,
                cat => cat.Id,
                (x, cat) => new CommissionEarningDto
                {
                    Id                 = x.Earning.Id,
                    CommissionTypeName = x.CommType.Name,
                    CategoryName       = cat.Name,
                    Description        = x.Earning.Notes,
                    Amount             = x.Earning.Amount,
                    Status             = x.Earning.Status.ToString(),
                    EarnedDate         = x.Earning.EarnedDate,
                    PaymentDate        = x.Earning.PaymentDate,
                    PeriodDate         = x.Earning.PeriodDate
                });

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.EarnedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var result = new PagedResult<CommissionEarningDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<CommissionEarningDto>>.Success(result);
    }
}
