using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoMembersGrid;

/// <summary>
/// Lectura del grid de miembros que se dieron de alta durante una promo corporativa: filtros de columna, orden y
/// página viajan en <see cref="Grid"/> y se aplican sobre todos los miembros de la promo.
/// </summary>
public record GetPromoMembersGridQuery(string PromoId, GridDataRequest Grid)
    : IRequest<Result<PagedResult<PromoMemberDto>>>;
