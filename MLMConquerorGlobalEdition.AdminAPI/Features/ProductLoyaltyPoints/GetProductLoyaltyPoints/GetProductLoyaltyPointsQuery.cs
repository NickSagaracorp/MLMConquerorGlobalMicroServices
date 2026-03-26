using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductLoyaltyPoints;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.GetProductLoyaltyPoints;

public record GetProductLoyaltyPointsQuery(string ProductId)
    : IRequest<Result<IEnumerable<ProductLoyaltyPointsDto>>>;
