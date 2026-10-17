using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// Eventos corporativos y las inscripciones del equipo del que llama (Plan J). El sujeto de la ruta
/// es el EVENTO, nunca un miembro: el equipo sale del token.
/// </summary>
[ApiController]
[Route("api/v1/bizcenter/events")]
[Authorize]
public class TeamEventsController : ControllerBase
{
    private readonly ITeamEventRegistrationsService _events;
    private readonly ICurrentUserService            _currentUser;

    public TeamEventsController(ITeamEventRegistrationsService events, ICurrentUserService currentUser)
    {
        _events      = events;
        _currentUser = currentUser;
    }

    /// <summary>GET /api/v1/bizcenter/events — eventos activos.</summary>
    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken ct = default) =>
        Ok(ApiResponse<List<TeamEventView>>.Ok(await _events.GetActiveEventsAsync(ct)));

    /// <summary>POST /api/v1/bizcenter/events/{eventId}/team-registrations/grid?registeredOnly=true</summary>
    /// <remarks>POST por el cuerpo de la rejilla, pero no escribe: ReadOnlySafe, como las de TeamController.</remarks>
    [HttpPost("{eventId}/team-registrations/grid")]
    [ReadOnlySafe]
    public async Task<IActionResult> GetTeamRegistrationsGrid(
        string eventId,
        [FromBody]  GridDataRequest request,
        [FromQuery] bool registeredOnly = true,
        CancellationToken ct = default)
    {
        var result = await _events.GetGridAsync(_currentUser.MemberId, eventId, registeredOnly, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PagedResult<TeamEventRegistrationView>>.Ok(result.Value!))
            : NotFound(ApiResponse<PagedResult<TeamEventRegistrationView>>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }

    /// <summary>GET /api/v1/bizcenter/events/{eventId}/team-registrations/totals</summary>
    [HttpGet("{eventId}/team-registrations/totals")]
    public async Task<IActionResult> GetTeamRegistrationsTotals(string eventId, CancellationToken ct = default)
    {
        var result = await _events.GetTotalsAsync(_currentUser.MemberId, eventId, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<TeamEventTotalsView>.Ok(result.Value!))
            : NotFound(ApiResponse<TeamEventTotalsView>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }
}
