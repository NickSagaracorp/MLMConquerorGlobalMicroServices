using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetAdminCommissionsGrid;

/// <summary>
/// Lectura del grid de comisiones del admin: <paramref name="Grid"/> trae filtros, orden y página de las columnas;
/// <paramref name="Status"/> es el desplegable de la cabecera de la página.
/// </summary>
public record GetAdminCommissionsGridQuery(GridDataRequest Grid, string? Status = null)
    : IRequest<Result<PagedResult<AdminCommissionDto>>>;
