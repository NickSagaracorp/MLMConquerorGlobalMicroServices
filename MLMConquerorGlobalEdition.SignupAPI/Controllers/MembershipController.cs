using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.CancelMembership;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.DowngradeMembership;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.UpgradeMembership;

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

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
        var result = await _mediator.Send(new UpgradeMembershipCommand(memberId, request.NewMembershipLevelId, request.Reason), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Membership upgraded successfully."));
    }

    [HttpPost("downgrade")]
    public async Task<IActionResult> Downgrade(string memberId, [FromBody] MembershipChangeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new DowngradeMembershipCommand(memberId, request.NewMembershipLevelId, request.Reason), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Membership downgraded successfully."));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(string memberId, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelMembershipCommand(memberId, null), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Membership cancelled successfully."));
    }
}
