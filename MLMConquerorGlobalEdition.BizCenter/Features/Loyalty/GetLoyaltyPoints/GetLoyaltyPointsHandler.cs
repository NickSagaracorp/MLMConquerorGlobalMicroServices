using MediatR;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Services.Loyalty;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Loyalty.GetLoyaltyPoints;

/// <summary>El miembro sale del token; la consulta es la del servicio compartido con el perfil de miembro del Admin.</summary>
public class GetLoyaltyPointsHandler : IRequestHandler<GetLoyaltyPointsQuery, Result<LoyaltyPointsPageView>>
{
    private readonly ICurrentUserService   _currentUser;
    private readonly IMemberLoyaltyService _loyalty;

    public GetLoyaltyPointsHandler(ICurrentUserService currentUser, IMemberLoyaltyService loyalty)
    {
        _currentUser = currentUser;
        _loyalty     = loyalty;
    }

    public async Task<Result<LoyaltyPointsPageView>> Handle(GetLoyaltyPointsQuery request, CancellationToken ct) =>
        Result<LoyaltyPointsPageView>.Success(
            await _loyalty.GetPageAsync(_currentUser.MemberId, request.Page, request.PageSize, ct));
}
