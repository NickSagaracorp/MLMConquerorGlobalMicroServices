using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetFlaggedSignupsGrid;

/// <summary>
/// Lectura del grid de "Flagged Signups". Los filtros de la cabecera de la página (visitante, IP, patrocinador,
/// fechas, solo marcados, incluir liberados) van aparte y se aplican sobre la tabla; filtros de columna, orden y
/// página viajan en <see cref="Grid"/>.
/// </summary>
public record GetFlaggedSignupsGridQuery(
    GridDataRequest Grid,
    string?   VisitorId            = null,
    string?   IpAddress            = null,
    string?   SponsorReplicateSite = null,
    DateTime? From                 = null,
    DateTime? To                   = null,
    bool      OnlyFlagged          = false,
    bool      IncludeCleared       = false
) : IRequest<Result<PagedResult<FlaggedSignupDto>>>;
