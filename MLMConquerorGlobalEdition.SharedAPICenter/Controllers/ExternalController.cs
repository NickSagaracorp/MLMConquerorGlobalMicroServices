using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.GetExternalMemberProfile;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.GetExternalMemberRank;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.ValidateMemberToken;
using MLMConquerorGlobalEdition.SharedAPICenter.Seguridad;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Controllers;

/// <summary>
/// Exposes read-only member data to trusted external systems.
///
/// Autenticación: cabecera X-Api-Key contra ExternalApi:ApiKey, por el esquema
/// <see cref="ApiKeyExternaHandler"/> (comparación en tiempo constante; sin llave configurada,
/// 401 para todos).
/// </summary>
[ApiController]
[Route("api/v1/external")]
[Authorize(AuthenticationSchemes = ApiKeyExternaHandler.Esquema)]
public class ExternalController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExternalController(IMediator mediator) => _mediator = mediator;


    /// <summary>
    /// Returns a slim public profile for the given member.
    /// </summary>
    /// <param name="memberId">Human-readable member ID, e.g. AMB-000001.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("member/{memberId}/profile")]
    [ProducesResponseType(typeof(ApiResponse<ExternalMemberProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberProfile(
        [FromRoute] string memberId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetExternalMemberProfileQuery(memberId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<ExternalMemberProfileDto>.Ok(result.Value!));
    }

    /// <summary>
    /// Returns the current rank for the given member.
    /// If the member exists but has not yet achieved a rank, rank fields will be null.
    /// </summary>
    /// <param name="memberId">Human-readable member ID, e.g. AMB-000001.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("member/{memberId}/rank")]
    [ProducesResponseType(typeof(ApiResponse<ExternalMemberRankDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberRank(
        [FromRoute] string memberId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetExternalMemberRankQuery(memberId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<ExternalMemberRankDto>.Ok(result.Value!));
    }

    /// <summary>
    /// Validates whether a member holds sufficient balance of the specified token type.
    /// </summary>
    /// <param name="request">MemberId, TokenTypeId, and RequiredQuantity.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("member/validate-token")]
    [ProducesResponseType(typeof(ApiResponse<ValidateTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateToken(
        [FromBody] ValidateTokenRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ValidateMemberTokenCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<ValidateTokenResponse>.Ok(result.Value!));
    }
}
