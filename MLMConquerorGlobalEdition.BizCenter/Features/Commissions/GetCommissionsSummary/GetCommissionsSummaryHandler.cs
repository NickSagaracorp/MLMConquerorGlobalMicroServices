using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsSummary;

public class GetCommissionsSummaryHandler : IRequestHandler<GetCommissionsSummaryQuery, Result<CommissionSummaryDto>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetCommissionsSummaryHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<CommissionSummaryDto>> Handle(GetCommissionsSummaryQuery request, CancellationToken ct)
    {
        var v = await _service.GetSummaryAsync(_currentUser.MemberId, ct);

        return Result<CommissionSummaryDto>.Success(new CommissionSummaryDto
        {
            PendingTotal     = v.PendingTotal,
            PaidTotal        = v.PaidTotal,
            CurrentYearTotal = v.CurrentYearTotal
        });
    }
}
