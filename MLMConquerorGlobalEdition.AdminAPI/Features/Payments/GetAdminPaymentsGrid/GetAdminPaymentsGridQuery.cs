using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payments;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payments.GetAdminPaymentsGrid;

/// <summary>
/// Lectura del grid de servidor de Payment History. Pagina, orden, filtros de columna y busqueda viajan en
/// <paramref name="Grid"/>; el estado, el miembro y el rango de fechas de la cabecera de la pagina viajan aparte y se
/// aplican sobre la tabla. <paramref name="To"/> incluye el dia entero.
/// </summary>
public record GetAdminPaymentsGridQuery(
    GridDataRequest Grid,
    string?   StatusFilter = null,
    string?   MemberId     = null,
    DateTime? From         = null,
    DateTime? To           = null
) : IRequest<Result<PagedResult<AdminPaymentDto>>>;
