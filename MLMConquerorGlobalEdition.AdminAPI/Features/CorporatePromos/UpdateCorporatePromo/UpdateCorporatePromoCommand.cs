using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.UpdateCorporatePromo;

public record UpdateCorporatePromoCommand(string Id, UpdateCorporatePromoRequest Request) : IRequest<Result<bool>>;
