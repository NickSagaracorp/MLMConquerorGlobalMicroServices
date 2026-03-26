using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductLoyaltyPoints;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.UpdateProductLoyaltyPoints;

public record UpdateProductLoyaltyPointsCommand(string ProductId, int Id, UpdateProductLoyaltyPointsRequest Request)
    : IRequest<Result<bool>>;
