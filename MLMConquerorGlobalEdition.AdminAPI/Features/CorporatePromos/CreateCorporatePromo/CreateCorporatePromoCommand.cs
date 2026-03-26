using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.CreateCorporatePromo;

public record CreateCorporatePromoCommand(CreateCorporatePromoRequest Request) : IRequest<Result<string>>;
