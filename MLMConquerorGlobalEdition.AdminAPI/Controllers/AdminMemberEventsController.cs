using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Eventos corporativos y las inscripciones del equipo del miembro de la ruta, para el perfil de
/// miembro del Admin. Espejo de <c>TeamEventsController</c> del BizCenter: mismo servicio, mismas
/// respuestas; el equipo sale de la ruta y no del token.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/events")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberEventsController : ControllerBase
{
    private readonly ITeamEventRegistrationsService _events;

    public AdminMemberEventsController(ITeamEventRegistrationsService events) => _events = events;

    /// <summary>GET .../events — eventos activos.</summary>
    [HttpGet]
    public async Task<IActionResult> GetActive(string memberId, CancellationToken ct = default) =>
        Ok(ApiResponse<List<TeamEventView>>.Ok(await _events.GetActiveEventsAsync(ct)));

    /// <summary>POST .../events/{eventId}/team-registrations/grid?registeredOnly=true</summary>
    [HttpPost("{eventId}/team-registrations/grid")]
    public async Task<IActionResult> GetTeamRegistrationsGrid(
        string memberId,
        string eventId,
        [FromBody]  GridDataRequest request,
        [FromQuery] bool registeredOnly = true,
        CancellationToken ct = default)
    {
        var result = await _events.GetGridAsync(memberId, eventId, registeredOnly, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PagedResult<TeamEventRegistrationView>>.Ok(result.Value!))
            : NotFound(ApiResponse<PagedResult<TeamEventRegistrationView>>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }

    /// <summary>GET .../events/{eventId}/team-registrations/totals</summary>
    [HttpGet("{eventId}/team-registrations/totals")]
    public async Task<IActionResult> GetTeamRegistrationsTotals(string memberId, string eventId, CancellationToken ct = default)
    {
        var result = await _events.GetTotalsAsync(memberId, eventId, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<TeamEventTotalsView>.Ok(result.Value!))
            : NotFound(ApiResponse<TeamEventTotalsView>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }
}
