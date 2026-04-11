using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetProducts;

/// <param name="CountryIso2">
/// Optional 2-letter ISO country code. When provided, only products mapped to that
/// country are returned. When omitted (or the country has no mappings), all active
/// JoinPageMembership products are returned.
/// </param>
public record GetProductsQuery(string? CountryIso2 = null)
    : IRequest<Result<IEnumerable<ProductDto>>>;
