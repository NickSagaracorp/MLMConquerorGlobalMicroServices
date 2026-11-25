using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Subscriptions;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Subscriptions.GetSubscriptionsGrid;

/// <summary>
/// Lectura del grid de servidor de Subscriptions: la pagina, el orden, los filtros de columna y la busqueda viajan en
/// <paramref name="Grid"/>; el estado del desplegable de la pagina viaja aparte para que se aplique sobre la tabla.
/// </summary>
public record GetSubscriptionsGridQuery(GridDataRequest Grid, string? StatusFilter = null)
    : IRequest<Result<PagedResult<SubscriptionDto>>>;
