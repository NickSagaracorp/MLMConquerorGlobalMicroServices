using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusCommissions;

public class GetPresidentialBonusCommissionsHandler : IRequestHandler<GetPresidentialBonusCommissionsQuery, Result<PagedResult<CommissionEarningDto>>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetPresidentialBonusCommissionsHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<CommissionEarningDto>>> Handle(GetPresidentialBonusCommissionsQuery request, CancellationToken ct)
    {
        var v = await _service.GetPresidentialBonusAsync(
            _currentUser.MemberId, request.Page, request.PageSize, ct);

        var result = new PagedResult<CommissionEarningDto>
        {
            Items = v.Items.Select(e => new CommissionEarningDto
            {
                Id                 = e.Id,
                CommissionTypeName = e.CommissionTypeName,
                CategoryName       = e.CategoryName,
                Description        = e.Description,
                Amount             = e.Amount,
                Status             = e.Status,
                EarnedDate         = e.EarnedDate,
                PaymentDate        = e.PaymentDate,
                PeriodDate         = e.PeriodDate
            }).ToList(),
            TotalCount = v.TotalCount,
            Page       = v.Page,
            PageSize   = v.PageSize
        };

        return Result<PagedResult<CommissionEarningDto>>.Success(result);
    }
}
