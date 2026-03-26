using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetCorporatePromoById;

public record GetCorporatePromoByIdQuery(string Id) : IRequest<Result<CorporatePromoDto>>;
