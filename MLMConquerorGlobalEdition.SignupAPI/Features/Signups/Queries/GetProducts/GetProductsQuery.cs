using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetProducts;

public record GetProductsQuery : IRequest<Result<IEnumerable<ProductDto>>>;
