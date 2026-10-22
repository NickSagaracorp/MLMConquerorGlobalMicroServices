using MediatR;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Services.Loyalty;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Loyalty.GetLoyaltyPoints;

/// <summary>Puntos de lealtad del miembro del token; la consulta es la misma que usa el Admin con el miembro de la ruta.</summary>
public class GetLoyaltyPointsHandler : IRequestHandler<GetLoyaltyPointsQuery, Result<LoyaltyPointsPageView>>
{
    private readonly ILoyaltyPointsService _loyalty;
    private readonly ICurrentUserService _currentUser;

    public GetLoyaltyPointsHandler(ILoyaltyPointsService loyalty, ICurrentUserService currentUser)
    {
        _loyalty = loyalty;
        _currentUser = currentUser;
    }

    public async Task<Result<LoyaltyPointsPageView>> Handle(GetLoyaltyPointsQuery request, CancellationToken ct) =>
        Result<LoyaltyPointsPageView>.Success(
            await _loyalty.GetPageAsync(_currentUser.MemberId, request.Page, request.PageSize, ct));
}
