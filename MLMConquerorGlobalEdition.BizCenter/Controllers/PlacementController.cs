using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetAvailableNodes;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetPendingPlacements;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetPlacementHistory;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.RemovePlacement;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// La colocación en el árbol binario, desde el centro de negocios.
/// </summary>
/// <remarks>
/// <c>IsAdmin</c> RELAJABA LÍMITES, NO COMPROBABA PROPIEDAD. Los dos manejadores
/// —<c>PlaceMemberHandler</c> y <c>RemovePlacementHandler</c>— leían <c>_currentUser.IsAdmin</c>
/// solo para saltarse la ventana de 30 días y el tope de dos oportunidades. Ni uno comprobaba que
/// el que llama tuviera algo que ver con el miembro que estaba colocando. Con eso, cualquier
/// cuenta autenticada podía colocar —o SACAR del árbol— a cualquier miembro escribiendo su
/// identificador en el cuerpo o en la URL, y colocar mueve puntos de pierna, que es dinero.
/// Mismo agujero y misma cura que <c>PlacementController</c> de SignupAPI en 4f4beaf.
///
/// LA REGLA ES EL PATROCINIO DIRECTO Y NO LA RED ENTERA. La pantalla que alimenta estas dos rutas
/// lista colocaciones PENDIENTES de los patrocinados directos del que mira; un patrocinado de un
/// patrocinado ya tiene a su propio patrocinador para eso. Y para el nodo DESTINO la regla es la
/// red de patrocinio, que es exactamente el conjunto que <c>available-nodes</c> ofrece en esa
/// misma pantalla: cerrarlo por menos rompería la pantalla, y por más dejaría colgar a un
/// patrocinado en la pierna de un desconocido.
///
/// El personal sigue pasando por encima, como en todo el resto: <see cref="CallerIdentity"/> lo
/// resuelve dentro de <see cref="IDownlineGuard"/> y no se repite aquí.
/// </remarks>
[ApiController]
[Route("api/v1/bizcenter/placement")]
[Authorize]
public class PlacementController : ControllerBase
{
    private readonly IMediator      _mediator;
    private readonly IDownlineGuard _propiedad;

    public PlacementController(IMediator mediator, IDownlineGuard propiedad)
    {
        _mediator  = mediator;
        _propiedad = propiedad;
    }

    /// <inheritdoc cref="TeamController"/>
    private IActionResult Ajeno(string detalle) =>
        StatusCode(StatusCodes.Status403Forbidden,
            ApiResponse<bool>.Fail("FORBIDDEN", detalle, HttpContext.TraceIdentifier));

    /// <summary>
    /// GET /api/v1/bizcenter/placement/pending
    /// Returns all enrolled members pending placement or within the correction window.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingPlacements(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPendingPlacementsQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<PendingPlacementDto>>.Fail(
                result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<IEnumerable<PendingPlacementDto>>.Ok(result.Value!));
    }

    /// <summary>
    /// GET /api/v1/bizcenter/placement/available-nodes/{memberToPlaceId}
    /// Returns the sponsor's Dual Team subtree with available slot information.
    /// </summary>
    [HttpGet("available-nodes/{memberToPlaceId}")]
    public async Task<IActionResult> GetAvailableNodes(
        string memberToPlaceId, CancellationToken ct)
    {
        if (!await _propiedad.PatrocinaAsync(User, memberToPlaceId, ct))
            return Ajeno("No patrocinas a ese miembro.");

        var result = await _mediator.Send(new GetAvailableNodesQuery(memberToPlaceId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AvailableNodesResponse>.Fail(
                result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AvailableNodesResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// POST /api/v1/bizcenter/placement
    /// Places a sponsored member into a specific Dual Team node.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PlaceMember(
        [FromBody] PlaceMemberRequest request, CancellationToken ct)
    {
        // Dos sujetos y dos preguntas distintas: a QUIÉN se coloca —tiene que ser un patrocinado
        // directo— y DEBAJO DE QUIÉN —tiene que ser alguien de tu red, que es justo lo que
        // available-nodes ofrece—. Cerrar solo la primera dejaría colgar a tu patrocinado en la
        // pierna de un desconocido, que le mueve los puntos a él y no a ti.
        if (!await _propiedad.PatrocinaAsync(User, request?.MemberToPlaceId, ct))
            return Ajeno("No patrocinas a ese miembro.");

        if (!await _propiedad.PuedeColocarBajoAsync(User, request!.TargetParentMemberId, ct))
            return Ajeno("Ese nodo no está en tu red.");

        var command = new PlaceMemberCommand(
            request.MemberToPlaceId,
            request.TargetParentMemberId,
            request.Side);

        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PlaceMemberResult>.Fail(
                result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PlaceMemberResult>.Ok(result.Value!,
            $"{result.Value!.FullName} ha sido colocado exitosamente en la pierna {result.Value.Side}."));
    }

    /// <summary>
    /// DELETE /api/v1/bizcenter/placement/{memberToRemoveId}
    /// Removes the placement of a member (within 72h correction window).
    /// </summary>
    [HttpDelete("{memberToRemoveId}")]
    public async Task<IActionResult> RemovePlacement(
        string memberToRemoveId, CancellationToken ct)
    {
        if (!await _propiedad.PatrocinaAsync(User, memberToRemoveId, ct))
            return Ajeno("No patrocinas a ese miembro.");

        var result = await _mediator.Send(new RemovePlacementCommand(memberToRemoveId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<RemovePlacementResult>.Fail(
                result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<RemovePlacementResult>.Ok(result.Value!,
            $"Placement de {result.Value!.FullName} eliminado. " +
            $"Oportunidades restantes: {result.Value.OpportunitiesRemaining}."));
    }

    /// <summary>
    /// GET /api/v1/bizcenter/placement/history
    /// Returns paginated placement history for the current ambassador's sponsored members.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetPlacementHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPlacementHistoryQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<PlacementHistoryDto>>.Fail(
                result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<PlacementHistoryDto>>.Ok(result.Value!));
    }
}
