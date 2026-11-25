using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.GetAdminTicketsGrid;

/// <summary>
/// Lectura del grid de tickets del admin: filtros de columna, orden y página viajan en <see cref="Grid"/> y se
/// aplican en el servidor sobre TODOS los tickets. <see cref="Status"/> es el desplegable de la página, que va aparte
/// para que se aplique sobre la tabla antes de proyectar.
/// </summary>
public record GetAdminTicketsGridQuery(GridDataRequest Grid, string? Status = null)
    : IRequest<Result<PagedResult<AdminTicketDto>>>;
