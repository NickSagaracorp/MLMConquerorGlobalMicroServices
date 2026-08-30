using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.PlaceMember;
using MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.UnplaceMember;
using MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Queries.ValidatePlacement;

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

/// <summary>
/// La posición de un miembro en el árbol binario.
/// </summary>
/// <remarks>
/// MISMO AGUJERO QUE <see cref="MembershipController"/> y con la misma cura: el <c>[Authorize]</c>
/// pelado solo comprobaba que hubiera sesión, el sujeto salía del <c>{memberId}</c> de la ruta y
/// ningún manejador miraba el token. Cualquier cuenta autenticada podía recolocar —o SACAR— del
/// árbol a cualquier otro miembro, que en este negocio es mover dinero de sitio.
///
/// <c>requestedBy</c> DE LA BAJA ERA PARTE DEL PROBLEMA: <c>User.Identity?.Name ?? memberId</c>
/// caía al propio identificador de la ruta cuando el token no traía nombre, así que la auditoría
/// registraba como autor al miembro al que le estaban haciendo la operación. Ahora sale del token
/// y solo del token.
/// </remarks>
[ApiController]
[Route("api/v1/members/{memberId}/placement")]
[Authorize]
public class PlacementController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlacementController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Place(string memberId, [FromBody] PlacementRequest request, CancellationToken ct)
    {
        if (!User.CanActOnMember(memberId)) return Ajeno();

        var result = await _mediator.Send(new PlaceMemberCommand(memberId, request.PlaceUnderMemberId, request.Side), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Member placed successfully."));
    }

    [HttpDelete]
    public async Task<IActionResult> Unplace(string memberId, CancellationToken ct)
    {
        if (!User.CanActOnMember(memberId)) return Ajeno();

        var requestedBy = CallerIdentity.UserIdOf(User) ?? User.Identity?.Name ?? string.Empty;
        var result = await _mediator.Send(new UnplaceMemberCommand(memberId, requestedBy), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Member unplaced successfully."));
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(string memberId, [FromBody] PlacementRequest request, CancellationToken ct)
    {
        if (!User.CanActOnMember(memberId)) return Ajeno();

        var result = await _mediator.Send(new ValidatePlacementQuery(memberId, request.PlaceUnderMemberId, request.Side), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Placement position is available."));
    }

    /// <inheritdoc cref="MembershipController"/>
    private IActionResult Ajeno() =>
        StatusCode(StatusCodes.Status403Forbidden,
            ApiResponse<bool>.Fail("FORBIDDEN", "Esta posición no es de tu cuenta."));
}
