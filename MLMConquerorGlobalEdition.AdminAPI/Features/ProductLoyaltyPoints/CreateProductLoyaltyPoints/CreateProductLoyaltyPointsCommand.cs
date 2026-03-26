using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductLoyaltyPoints;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.CreateProductLoyaltyPoints;

public record CreateProductLoyaltyPointsCommand(string ProductId, CreateProductLoyaltyPointsRequest Request)
    : IRequest<Result<int>>;
