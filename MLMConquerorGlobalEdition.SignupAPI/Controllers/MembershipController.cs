using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.CancelMembership;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.DowngradeMembership;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.UpgradeMembership;

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

/// <summary>
/// La membresía de un miembro, gestionada por él mismo.
/// </summary>
/// <remarks>
/// EL <c>[Authorize]</c> DE ARRIBA NO BASTABA, y este era el agujero: comprueba que hay sesión y
/// nunca de quién es. El sujeto de las tres operaciones venía del <c>{memberId}</c> de la RUTA y
/// ningún manejador miraba el token, así que cualquier cuenta autenticada podía subir, bajar o
/// CANCELAR la membresía de cualquier otro miembro cambiando una cadena en la URL.
///
/// La regla que se aplica ahora es la de <see cref="CallerIdentity.CanActOnMember"/>: el
/// <c>{memberId}</c> tiene que ser el del token, salvo que quien llame sea personal. Que el personal
/// pase por encima no abre nada nuevo — la superficie administrativa
/// <c>/api/v1/admin/members/{memberId}/membership</c> ya existe con sus roles—, y esa ruta paralela
/// es justamente lo que demuestra que ESTA es la de autoservicio.
/// </remarks>
[ApiController]
[Route("api/v1/members/{memberId}/membership")]
[Authorize]
public class MembershipController : ControllerBase
{
    private readonly IMediator _mediator;

    public MembershipController(IMediator mediator) => _mediator = mediator;

    [HttpPost("upgrade")]
    public async Task<IActionResult> Upgrade(string memberId, [FromBody] MembershipChangeRequest request, CancellationToken ct)
    {
        if (!User.CanActOnMember(memberId)) return Ajeno();

        var result = await _mediator.Send(new UpgradeMembershipCommand(memberId, request.NewMembershipLevelId, request.Reason), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Membership upgraded successfully."));
    }

    [HttpPost("downgrade")]
    public async Task<IActionResult> Downgrade(string memberId, [FromBody] MembershipChangeRequest request, CancellationToken ct)
    {
        if (!User.CanActOnMember(memberId)) return Ajeno();

        var result = await _mediator.Send(new DowngradeMembershipCommand(memberId, request.NewMembershipLevelId, request.Reason), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Membership downgraded successfully."));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(string memberId, CancellationToken ct)
    {
        if (!User.CanActOnMember(memberId)) return Ajeno();

        var result = await _mediator.Send(new CancelMembershipCommand(memberId, null), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Membership cancelled successfully."));
    }

    /// <summary>
    /// 403 y no 404: quien llama está autenticado y la ruta existe, lo que no tiene es permiso. El
    /// cuerpo va en el mismo sobre que el resto para que el cliente no tenga que distinguir formas.
    /// </summary>
    private IActionResult Ajeno() =>
        StatusCode(StatusCodes.Status403Forbidden,
            ApiResponse<bool>.Fail("FORBIDDEN", "Esta membresía no es de tu cuenta."));
}
