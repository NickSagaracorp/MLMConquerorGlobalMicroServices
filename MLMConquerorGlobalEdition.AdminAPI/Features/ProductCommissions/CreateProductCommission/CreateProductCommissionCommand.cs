using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductCommissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.CreateProductCommission;

public record CreateProductCommissionCommand(CreateProductCommissionRequest Request)
    : IRequest<Result<int>>;
