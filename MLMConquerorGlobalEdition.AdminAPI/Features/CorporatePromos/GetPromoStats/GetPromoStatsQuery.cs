using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoStats;

public record GetPromoStatsQuery(string PromoId) : IRequest<Result<PromoStatsDto>>;
