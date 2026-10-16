using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Actividad por fechas del equipo de inscripción (Plan J, bloque 4).</summary>
public interface IEnrollmentActivityService
{
    Task<List<EnrollmentActivityColumnView>> GetColumnsAsync(CancellationToken ct = default);

    /// <summary>
    /// <paramref name="from"/> y <paramref name="to"/> son días inclusivos y siguen la regla de
    /// actividad de <see cref="ReportDateRange.TryCreateForActivity"/>: falla con
    /// REPORT_RANGE_INVALID, REPORT_RANGE_TOO_OLD o REPORT_RANGE_IN_FUTURE.
    /// </summary>
    Task<Result<PagedResult<EnrollmentActivityRowView>>> GetGridAsync(
        string memberId, DateTime from, DateTime to, GridDataRequest request, CancellationToken ct = default);
}
