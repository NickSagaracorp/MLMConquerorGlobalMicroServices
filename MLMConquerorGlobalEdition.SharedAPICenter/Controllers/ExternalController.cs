using MediatR;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.GetExternalMemberProfile;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.GetExternalMemberRank;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.ValidateMemberToken;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Controllers;

/// <summary>
/// Exposes read-only member data to trusted external systems.
///
/// Authentication: X-Api-Key header validated against IConfiguration["ExternalApi:ApiKey"].
/// All three endpoints perform the check inline and return 401 when the key is missing
/// or invalid.
/// </summary>
[ApiController]
[Route("api/v1/external")]
public class ExternalController : ControllerBase
{
    private readonly IMediator      _mediator;
    private readonly IConfiguration _config;

    public ExternalController(IMediator mediator, IConfiguration config)
    {
        _mediator = mediator;
        _config   = config;
    }

    // ── Private helper ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the X-Api-Key header matches the configured secret.
    /// </summary>
    private bool IsApiKeyValid()
    {
        var expected = _config["ExternalApi:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected)) return false;

        Request.Headers.TryGetValue("X-Api-Key", out var provided);
        return string.Equals(provided, expected, StringComparison.Ordinal);
    }

    // ── Endpoints ─────────────────────────────────────────────────────────────

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
        if (!IsApiKeyValid())
            return Unauthorized(ApiResponse<object>.Fail(
                "UNAUTHORIZED", "Invalid or missing API key.", HttpContext.TraceIdentifier));

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
        if (!IsApiKeyValid())
            return Unauthorized(ApiResponse<object>.Fail(
                "UNAUTHORIZED", "Invalid or missing API key.", HttpContext.TraceIdentifier));

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
        if (!IsApiKeyValid())
            return Unauthorized(ApiResponse<object>.Fail(
                "UNAUTHORIZED", "Invalid or missing API key.", HttpContext.TraceIdentifier));

        var result = await _mediator.Send(new ValidateMemberTokenCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<ValidateTokenResponse>.Ok(result.Value!));
    }
}
