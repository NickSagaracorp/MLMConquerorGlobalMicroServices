using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs;
using MLMConquerorGlobalEdition.Signups.Features.Placement.Commands.PlaceMember;
using MLMConquerorGlobalEdition.Signups.Features.Placement.Commands.UnplaceMember;
using MLMConquerorGlobalEdition.Signups.Features.Placement.Queries.ValidatePlacement;

namespace MLMConquerorGlobalEdition.Signups.Controllers;

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
        var result = await _mediator.Send(new PlaceMemberCommand(memberId, request.PlaceUnderMemberId, request.Side), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Member placed successfully."));
    }

    [HttpDelete]
    public async Task<IActionResult> Unplace(string memberId, CancellationToken ct)
    {
        var requestedBy = User.Identity?.Name ?? memberId;
        var result = await _mediator.Send(new UnplaceMemberCommand(memberId, requestedBy), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Member unplaced successfully."));
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(string memberId, [FromBody] PlacementRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ValidatePlacementQuery(memberId, request.PlaceUnderMemberId, request.Side), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Placement position is available."));
    }
}
