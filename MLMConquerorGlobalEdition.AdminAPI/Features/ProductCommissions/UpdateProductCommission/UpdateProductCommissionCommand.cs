using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductCommissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.UpdateProductCommission;

public record UpdateProductCommissionCommand(int Id, UpdateProductCommissionRequest Request)
    : IRequest<Result<bool>>;
