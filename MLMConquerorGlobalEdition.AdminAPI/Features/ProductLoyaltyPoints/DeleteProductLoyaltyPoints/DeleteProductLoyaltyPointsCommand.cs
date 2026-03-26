using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.DeleteProductLoyaltyPoints;

public record DeleteProductLoyaltyPointsCommand(string ProductId, int Id) : IRequest<Result<bool>>;
