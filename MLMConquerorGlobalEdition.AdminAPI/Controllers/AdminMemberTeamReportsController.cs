using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Reportes del equipo (actividad por fechas y cambios de estado) del miembro de la ruta, para el
/// perfil de miembro del Admin. Es el espejo de <c>TeamReportsController</c> del BizCenter: mismos
/// servicios, mismas respuestas; solo cambia de dónde sale el miembro (aquí la ruta, allí el token).
/// No añadir lógica de consulta aquí: se amplía el servicio compartido.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/team")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberTeamReportsController : ControllerBase
{
    private readonly IDateTimeProvider _clock;

    public AdminMemberTeamReportsController(IDateTimeProvider clock) => _clock = clock;

    /// <summary>GET .../team/reports/setup — rango por defecto, tope de días y primer día de Actividad (hora del servidor).</summary>
    [HttpGet("reports/setup")]
    public IActionResult GetSetup(string memberId) =>
        Ok(ApiResponse<ReportSetupView>.Ok(ReportDateRange.SetupFor(_clock.Now)));

    /// <summary>GET .../team/enrollment/activity/columns — nombres de los niveles 1 a 4.</summary>
    [HttpGet("enrollment/activity/columns")]
    public async Task<IActionResult> GetActivityColumns(
        string memberId, [FromServices] IEnrollmentActivityService activity, CancellationToken ct = default) =>
        Ok(ApiResponse<List<EnrollmentActivityColumnView>>.Ok(await activity.GetColumnsAsync(ct)));

    /// <summary>POST .../team/enrollment/activity/grid?from=yyyy-MM-dd&amp;to=yyyy-MM-dd</summary>
    [HttpPost("enrollment/activity/grid")]
    public async Task<IActionResult> GetActivityGrid(
        string memberId,
        [FromBody]  GridDataRequest request,
        [FromQuery] DateTime        from,
        [FromQuery] DateTime        to,
        [FromServices] IEnrollmentActivityService activity,
        CancellationToken ct = default)
    {
        var result = await activity.GetGridAsync(memberId, from, to, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PagedResult<EnrollmentActivityRowView>>.Ok(result.Value!))
            : Invalid(result);
    }

    /// <summary>POST .../team/changes/{scope}/grid?from=&amp;to= con scope = enrollment | dual | customers.</summary>
    [HttpPost("changes/{scope}/grid")]
    public async Task<IActionResult> GetStatusChangesGrid(
        string memberId,
        string scope,
        [FromBody]  GridDataRequest request,
        [FromQuery] DateTime        from,
        [FromQuery] DateTime        to,
        [FromServices] ITeamStatusChangesService changes,
        CancellationToken ct = default)
    {
        if (!StatusChangesScopes.TryParse(scope, out var parsed))
            return NotFound(ApiResponse<PagedResult<TeamStatusChangeView>>.Fail(
                "REPORT_SCOPE_NOT_FOUND", "Unknown report.", HttpContext.TraceIdentifier));

        var result = await changes.GetGridAsync(memberId, parsed, from, to, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PagedResult<TeamStatusChangeView>>.Ok(result.Value!))
            : Invalid(result);
    }

    private IActionResult Invalid<T>(Result<T> result) =>
        BadRequest(ApiResponse<T>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
}
