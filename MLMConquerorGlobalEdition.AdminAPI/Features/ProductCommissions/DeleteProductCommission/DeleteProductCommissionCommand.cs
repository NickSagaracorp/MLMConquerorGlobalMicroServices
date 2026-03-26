using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.DeleteProductCommission;

public record DeleteProductCommissionCommand(int Id) : IRequest<Result<bool>>;
