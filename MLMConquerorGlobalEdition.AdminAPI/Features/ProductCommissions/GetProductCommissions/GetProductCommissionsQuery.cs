using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductCommissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.GetProductCommissions;

public record GetProductCommissionsQuery(string? ProductId, PagedRequest Page)
    : IRequest<Result<PagedResult<ProductCommissionDto>>>;
