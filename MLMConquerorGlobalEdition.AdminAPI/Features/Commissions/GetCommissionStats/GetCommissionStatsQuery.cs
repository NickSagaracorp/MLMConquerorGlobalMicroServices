using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetCommissionStats;

/// <param name="BypassCache">"Refresh" de la pantalla: recalcula en vez de leer la caché.</param>
public record GetCommissionStatsQuery(bool BypassCache = false) : IRequest<Result<CommissionStatsDto>>;
