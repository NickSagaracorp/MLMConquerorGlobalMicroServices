using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;
using IDateTimeProvider   = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// Reportes del equipo del Plan J (actividad por fechas y, en el bloque 5, cambios de estado).
/// Ninguna ruta lleva sujeto: el miembro sale SIEMPRE del token (ICurrentUserService.MemberId) y
/// el alcance lo pone el servicio (prefijo de genealogía o del árbol binario). Comparte el prefijo
/// de ruta con <see cref="TeamController"/> sin pisar ninguna de sus rutas.
/// </summary>
[ApiController]
[Route("api/v1/bizcenter/team")]
[Authorize]
public class TeamReportsController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public TeamReportsController(ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _currentUser = currentUser;
        _clock       = clock;
    }

    /// <summary>
    /// GET /api/v1/bizcenter/team/reports/setup — rango por defecto (día 1 del mes a hoy), tope de
    /// días de la regla común y primer día admitido en Actividad, con la hora del servidor.
    /// </summary>
    [HttpGet("reports/setup")]
    public IActionResult GetSetup() =>
        Ok(ApiResponse<ReportSetupView>.Ok(ReportDateRange.SetupFor(_clock.Now)));

    /// <summary>GET /api/v1/bizcenter/team/enrollment/activity/columns — nombres de los niveles 1 a 4.</summary>
    [HttpGet("enrollment/activity/columns")]
    public async Task<IActionResult> GetActivityColumns(
        [FromServices] IEnrollmentActivityService activity, CancellationToken ct = default) =>
        Ok(ApiResponse<List<EnrollmentActivityColumnView>>.Ok(await activity.GetColumnsAsync(ct)));

    /// <summary>POST /api/v1/bizcenter/team/enrollment/activity/grid?from=yyyy-MM-dd&amp;to=yyyy-MM-dd</summary>
    /// <remarks>POST por el cuerpo de la rejilla, pero no escribe: ReadOnlySafe, como las de TeamController.</remarks>
    [HttpPost("enrollment/activity/grid")]
    [ReadOnlySafe]
    public async Task<IActionResult> GetActivityGrid(
        [FromBody]  GridDataRequest request,
        [FromQuery] DateTime        from,
        [FromQuery] DateTime        to,
        [FromServices] IEnrollmentActivityService activity,
        CancellationToken ct = default)
    {
        var result = await activity.GetGridAsync(_currentUser.MemberId, from, to, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PagedResult<EnrollmentActivityRowView>>.Ok(result.Value!))
            : Invalid(result);
    }

    private IActionResult Invalid<T>(Result<T> result) =>
        BadRequest(ApiResponse<T>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
}
