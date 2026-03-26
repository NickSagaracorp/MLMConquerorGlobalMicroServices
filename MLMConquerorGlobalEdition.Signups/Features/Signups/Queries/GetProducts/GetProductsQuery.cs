using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Queries.GetProducts;

public record GetProductsQuery : IRequest<Result<IEnumerable<ProductDto>>>;
