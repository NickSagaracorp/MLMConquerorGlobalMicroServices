using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.GhostPoints;
using MLMConquerorGlobalEdition.AdminAPI.Features.GhostPoints.CreateGhostPoint;
using MLMConquerorGlobalEdition.AdminAPI.Features.GhostPoints.DeleteGhostPoint;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/ghost-points")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class GhostPointsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GhostPointsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> CreateGhostPoint(
        [FromBody] CreateGhostPointRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateGhostPointCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<GhostPointDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<GhostPointDto>.Ok(result.Value!));
    }

    [HttpDelete("{ghostPointId}")]
    public async Task<IActionResult> DeleteGhostPoint(string ghostPointId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteGhostPointCommand(ghostPointId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Ghost point deactivated."));
    }
}
