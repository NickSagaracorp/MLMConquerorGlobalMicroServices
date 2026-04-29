using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusAmbassadors;

public class GetCarBonusAmbassadorsHandler : IRequestHandler<GetCarBonusAmbassadorsQuery, Result<List<CarBonusAmbassadorDto>>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetCarBonusAmbassadorsHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<List<CarBonusAmbassadorDto>>> Handle(GetCarBonusAmbassadorsQuery request, CancellationToken ct)
    {
        var items = await _service.GetCarBonusAmbassadorsAsync(
            _currentUser.MemberId, request.From, request.To, ct);

        var mapped = items.Select(v => new CarBonusAmbassadorDto
        {
            MemberId       = v.MemberId,
            AmbassadorName = v.AmbassadorName,
            TotalPoints    = v.TotalPoints,
            EligiblePoints = v.EligiblePoints
        }).ToList();

        return Result<List<CarBonusAmbassadorDto>>.Success(mapped);
    }
}
