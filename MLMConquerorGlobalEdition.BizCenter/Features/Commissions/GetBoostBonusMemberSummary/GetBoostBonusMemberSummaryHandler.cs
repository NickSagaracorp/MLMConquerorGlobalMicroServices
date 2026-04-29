using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusMemberSummary;

public class GetBoostBonusMemberSummaryHandler : IRequestHandler<GetBoostBonusMemberSummaryQuery, Result<BoostBonusMemberSummaryDto>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetBoostBonusMemberSummaryHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<BoostBonusMemberSummaryDto>> Handle(GetBoostBonusMemberSummaryQuery request, CancellationToken ct)
    {
        var v = await _service.GetBoostBonusMemberSummaryAsync(_currentUser.MemberId, ct);

        return Result<BoostBonusMemberSummaryDto>.Success(new BoostBonusMemberSummaryDto
        {
            TotalMembers      = v.TotalMembers,
            ActiveRebilling   = v.ActiveRebilling,
            InactiveRebilling = v.InactiveRebilling
        });
    }
}
