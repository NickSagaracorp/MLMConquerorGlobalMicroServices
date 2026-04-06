using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement;
using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminPlaceMember;
using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement;
using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.GetAdminPendingPlacements;
using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.RunAutoPlacement;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/placement")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminPlacementController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminPlacementController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Returns all members pending placement across the network.
    /// Optionally filter by sponsor.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingPlacements(
        [FromQuery] string? sponsorId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetAdminPendingPlacementsQuery(sponsorId), ct);

        return Ok(ApiResponse<IEnumerable<AdminPendingPlacementDto>>.Ok(result.Value!));
    }

    /// <summary>
    /// Places a member at a target node. Admin override — no time/opportunity restrictions.
    /// Structural rules (circular reference, no third leg, no auto-superiority) are still enforced.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PlaceMember(
        [FromBody] AdminPlaceMemberRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new AdminPlaceMemberCommand(
                request.MemberToPlaceId,
                request.TargetParentMemberId,
                request.Side), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<string>.Ok(result.Value!));
    }

    /// <summary>
    /// Removes a placement. Admin override — no time/opportunity restrictions.
    /// </summary>
    [HttpDelete("{memberId}")]
    public async Task<IActionResult> RemovePlacement(
        string memberId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new AdminRemovePlacementCommand(memberId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<string>.Ok(result.Value!));
    }

    /// <summary>
    /// Manually triggers the auto-placement job.
    /// Processes all members with expired placement windows who are not yet in the dual tree.
    /// </summary>
    [HttpPost("run-auto-placement")]
    public async Task<IActionResult> RunAutoPlacement(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RunAutoPlacementCommand(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<RunAutoPlacementResult>.Fail(
                result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<RunAutoPlacementResult>.Ok(result.Value!));
    }
}
