using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccessAudit;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccountChanges;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetFlaggedSignups;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetThirdParties;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.UnblockFingerprint;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/security")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class SecurityController : ControllerBase
{
    private readonly IMediator _mediator;

    public SecurityController(IMediator mediator) => _mediator = mediator;

    [HttpGet("access-audit")]
    public async Task<IActionResult> GetAccessAudit(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetAccessAuditQuery(new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<MemberStatusHistory>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<MemberStatusHistory>>.Ok(result.Value!));
    }

    [HttpGet("account-changes")]
    public async Task<IActionResult> GetAccountChanges(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetAccountChangesQuery(new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<AdminMemberDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<AdminMemberDto>>.Ok(result.Value!));
    }

    [HttpGet("/api/v1/admin/third-parties")]
    public async Task<IActionResult> GetThirdParties(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetThirdPartiesQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<string>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<IEnumerable<string>>.Ok(result.Value!));
    }

    /// <summary>
    /// List signup-risk fingerprint events for the AdminWeb security page. Use this to triage
    /// a customer who reports being blocked: filter by VisitorId/IP/Sponsor + time window.
    /// </summary>
    [HttpGet("flagged-signups")]
    public async Task<IActionResult> GetFlaggedSignups(
        [FromQuery] string?  visitorId            = null,
        [FromQuery] string?  ipAddress            = null,
        [FromQuery] string?  sponsorReplicateSite = null,
        [FromQuery] DateTime? from                = null,
        [FromQuery] DateTime? to                  = null,
        [FromQuery] bool     onlyFlagged          = false,
        [FromQuery] bool     includeCleared       = false,
        [FromQuery] int      page                 = 1,
        [FromQuery] int      pageSize             = 25,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFlaggedSignupsQuery(
            visitorId, ipAddress, sponsorReplicateSite, from, to,
            onlyFlagged, includeCleared, page, pageSize), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<FlaggedSignupDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<FlaggedSignupDto>>.Ok(result.Value!));
    }

    /// <summary>
    /// Clear fingerprint events for a visitor/IP so they stop counting toward the duplicate-threshold
    /// guard. Use this when support confirms the user is legitimate (not a hack attempt).
    /// </summary>
    [HttpPost("flagged-signups/unblock")]
    public async Task<IActionResult> UnblockFingerprint(
        [FromBody] UnblockFingerprintRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UnblockFingerprintCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<int>.Ok(result.Value, $"{result.Value} fingerprint row(s) cleared."));
    }
}
