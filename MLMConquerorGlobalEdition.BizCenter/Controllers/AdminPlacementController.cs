using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Jobs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// Admin-only operational endpoints for the dual-tree placement subsystem.
/// </summary>
[ApiController]
[Route("api/v1/bizcenter/admin/placement")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminPlacementController : ControllerBase
{
    private readonly AutoPlacementJob _job;

    public AdminPlacementController(AutoPlacementJob job) => _job = job;

    /// <summary>
    /// POST /api/v1/bizcenter/admin/placement/run-auto-now
    /// Forces an immediate run of the AutoPlacementJob. When ?ignoreWindow=true (default),
    /// bypasses the 30-day post-enrollment grace window so newly signed-up members get
    /// placed right away. Always recomputes LeftLegPoints / RightLegPoints / DualTeamPoints
    /// across the full tree at the end.
    /// </summary>
    [HttpPost("run-auto-now")]
    public async Task<IActionResult> RunAutoNow(
        [FromQuery] bool ignoreWindow = true,
        CancellationToken ct = default)
    {
        var placed = await _job.ExecuteAsync(ignoreWindow);
        return Ok(ApiResponse<RunAutoResult>.Ok(
            new RunAutoResult(placed, ignoreWindow),
            placed > 0
                ? $"Auto-placement completed — {placed} member(s) placed."
                : "Auto-placement completed — no members needed placement."));
    }

    public record RunAutoResult(int Placed, bool IgnoreWindow);
}
