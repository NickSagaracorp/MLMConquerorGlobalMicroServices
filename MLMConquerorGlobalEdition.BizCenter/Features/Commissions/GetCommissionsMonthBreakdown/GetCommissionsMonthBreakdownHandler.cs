using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsMonthBreakdown;

public class GetCommissionsMonthBreakdownHandler
    : IRequestHandler<GetCommissionsMonthBreakdownQuery, Result<List<CommissionMonthBreakdownDto>>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetCommissionsMonthBreakdownHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<List<CommissionMonthBreakdownDto>>> Handle(
        GetCommissionsMonthBreakdownQuery request, CancellationToken ct)
    {
        var groups = await _service.GetMonthBreakdownAsync(
            _currentUser.MemberId, request.Year, request.Month, ct);

        var mapped = groups.Select(g => new CommissionMonthBreakdownDto
        {
            CommissionTypeName = g.CommissionTypeName,
            Items = g.Items.Select(i => new CommissionMonthItemDto
            {
                EarnedDate  = i.EarnedDate,
                PaymentDate = i.PaymentDate,
                Detail      = i.Detail,
                Amount      = i.Amount,
                Status      = i.Status
            }).ToList()
        }).ToList();

        return Result<List<CommissionMonthBreakdownDto>>.Success(mapped);
    }
}
