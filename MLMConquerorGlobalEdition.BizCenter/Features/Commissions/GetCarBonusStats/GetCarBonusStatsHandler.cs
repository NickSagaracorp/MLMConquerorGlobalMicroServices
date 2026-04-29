using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusStats;

public class GetCarBonusStatsHandler : IRequestHandler<GetCarBonusStatsQuery, Result<CarBonusStatsDto>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetCarBonusStatsHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<CarBonusStatsDto>> Handle(GetCarBonusStatsQuery request, CancellationToken ct)
    {
        var v = await _service.GetCarBonusStatsAsync(_currentUser.MemberId, ct);

        return Result<CarBonusStatsDto>.Success(new CarBonusStatsDto
        {
            TotalPoints      = v.TotalPoints,
            EligiblePoints   = v.EligiblePoints,
            ProgressPercent  = v.ProgressPercent,
            TeamPointsTarget = v.TeamPointsTarget,
            MonthLabel       = v.MonthLabel
        });
    }
}
