using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissions;

public class GetCommissionsHandler : IRequestHandler<GetCommissionsQuery, Result<PagedResult<CommissionEarningDto>>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetCommissionsHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<CommissionEarningDto>>> Handle(GetCommissionsQuery request, CancellationToken ct)
    {
        var v = await _service.GetCommissionsAsync(
            _currentUser.MemberId,
            request.Page, request.PageSize,
            request.Status, request.From, request.To, ct);

        var result = new PagedResult<CommissionEarningDto>
        {
            Items      = v.Items.Select(MapEarning).ToList(),
            TotalCount = v.TotalCount,
            Page       = v.Page,
            PageSize   = v.PageSize
        };

        return Result<PagedResult<CommissionEarningDto>>.Success(result);
    }

    private static CommissionEarningDto MapEarning(CommissionEarningView v) => new()
    {
        Id                 = v.Id,
        CommissionTypeName = v.CommissionTypeName,
        CategoryName       = v.CategoryName,
        Description        = v.Description,
        Amount             = v.Amount,
        Status             = v.Status,
        EarnedDate         = v.EarnedDate,
        PaymentDate        = v.PaymentDate,
        PeriodDate         = v.PeriodDate
    };
}
