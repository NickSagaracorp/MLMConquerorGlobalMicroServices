using MediatR;
using MLMConquerorGlobalEdition.Repository.Services.Loyalty;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Loyalty.GetLoyaltyPoints;

public record GetLoyaltyPointsQuery(int Page, int PageSize) : IRequest<Result<LoyaltyPointsPageView>>;
