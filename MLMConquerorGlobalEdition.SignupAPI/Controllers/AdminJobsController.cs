using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

/// <summary>
/// Admin-only escape hatches for synchronously running the recurring sweeps
/// that normally fire from Hangfire. Useful when:
///   • Demoing — the data needs to land in the dashboard NOW, not on the
///     next 10-minute tick.
///   • A multi-server Hangfire cluster has temporarily quiesced because
///     the scheduler tick ran on a server without the SignupAPI assembly
///     loaded and the recurring job entered the retry-then-pause state.
/// Both routes invoke the job directly inside this process — they bypass
/// Hangfire entirely so they always have the assembly available.
/// </summary>
[ApiController]
[Route("api/v1/signups/admin/jobs")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminJobsController : ControllerBase
{
    [HttpPost("contest-points-sweep")]
    public async Task<IActionResult> RunContestPointsSweep(
        [FromServices] ContestPointsSweepJob job,
        CancellationToken ct = default)
    {
        await job.ExecuteAsync(ct);
        return Ok(ApiResponse<object>.Ok(
            new { triggeredAt = DateTime.UtcNow },
            "Contest points sweep executed."));
    }

    [HttpPost("builder-bonus-sweep")]
    public async Task<IActionResult> RunBuilderBonusSweep(
        [FromServices] BuilderBonusSweepJob job,
        CancellationToken ct = default)
    {
        await job.ExecuteAsync(ct);
        return Ok(ApiResponse<object>.Ok(
            new { triggeredAt = DateTime.UtcNow },
            "Builder bonus sweep executed."));
    }
}
