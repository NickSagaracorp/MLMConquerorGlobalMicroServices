using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Reporte de cambios de estado del equipo (Plan J, bloque 5).</summary>
public interface ITeamStatusChangesService
{
    /// <summary>
    /// Miembros del alcance con algún cambio registrado entre <paramref name="from"/> y
    /// <paramref name="to"/> (días inclusivos, regla común de <see cref="ReportDateRange"/>: 366 días).
    /// </summary>
    Task<Result<PagedResult<TeamStatusChangeView>>> GetGridAsync(
        string memberId, StatusChangesScope scope, DateTime from, DateTime to,
        GridDataRequest request, CancellationToken ct = default);
}
