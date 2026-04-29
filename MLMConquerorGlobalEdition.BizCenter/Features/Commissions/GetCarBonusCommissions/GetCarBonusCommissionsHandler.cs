using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusCommissions;

public class GetCarBonusCommissionsHandler : IRequestHandler<GetCarBonusCommissionsQuery, Result<PagedResult<CommissionEarningDto>>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetCarBonusCommissionsHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<CommissionEarningDto>>> Handle(GetCarBonusCommissionsQuery request, CancellationToken ct)
    {
        var v = await _service.GetCarBonusAsync(
            _currentUser.MemberId, request.Page, request.PageSize,
            request.From, request.To, ct);

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
