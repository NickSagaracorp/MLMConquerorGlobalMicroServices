using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.DeleteCorporatePromo;

public record DeleteCorporatePromoCommand(string Id) : IRequest<Result<bool>>;
