using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusWeekStats;

public class GetBoostBonusWeekStatsHandler : IRequestHandler<GetBoostBonusWeekStatsQuery, Result<BoostBonusWeekStatsDto>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetBoostBonusWeekStatsHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<BoostBonusWeekStatsDto>> Handle(GetBoostBonusWeekStatsQuery request, CancellationToken ct)
    {
        var v = await _service.GetBoostBonusWeekStatsAsync(_currentUser.MemberId, ct);

        return Result<BoostBonusWeekStatsDto>.Success(new BoostBonusWeekStatsDto
        {
            WeekLabel            = v.WeekLabel,
            GoldCount            = v.GoldCount,
            GoldTarget           = v.GoldTarget,
            PlatinumCount        = v.PlatinumCount,
            PlatinumTarget       = v.PlatinumTarget,
            GoldPoints           = v.GoldPoints,
            GoldPointsTarget     = v.GoldPointsTarget,
            PlatinumPoints       = v.PlatinumPoints,
            PlatinumPointsTarget = v.PlatinumPointsTarget
        });
    }
}
