using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetCorporatePromos;

public record GetCorporatePromosQuery(PagedRequest Page)
    : IRequest<Result<PagedResult<CorporatePromoDto>>>;
