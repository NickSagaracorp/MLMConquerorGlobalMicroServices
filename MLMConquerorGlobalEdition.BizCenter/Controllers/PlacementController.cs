using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetAvailableNodes;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetPendingPlacements;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetPlacementHistory;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.RemovePlacement;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/placement")]
[Authorize]
public class PlacementController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlacementController(IMediator mediator) => _mediator = mediator;

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
