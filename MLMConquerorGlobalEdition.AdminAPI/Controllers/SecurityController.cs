using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccessAudit;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccountChanges;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetThirdParties;
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
}
