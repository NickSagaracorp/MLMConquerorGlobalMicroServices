using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Loyalty;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Loyalty.GetLoyaltyPoints;

public record GetLoyaltyPointsQuery(int Page, int PageSize) : IRequest<Result<PagedResult<LoyaltyPointsDto>>>;
