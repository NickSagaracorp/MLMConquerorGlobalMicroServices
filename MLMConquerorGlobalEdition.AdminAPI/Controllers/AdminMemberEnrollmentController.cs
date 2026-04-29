using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Enrollment-team data for a specific member — used by Admin Member Profile.
/// All business logic lives in <see cref="IEnrollmentTeamService"/>; this
/// controller is a thin authorization + routing wrapper. Do NOT add query
/// logic here — keep the BizCenter and Admin views in lockstep by extending
/// the shared service instead.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/team/enrollment")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberEnrollmentController : ControllerBase
{
    private readonly IEnrollmentTeamService _service;

    public AdminMemberEnrollmentController(IEnrollmentTeamService service)
        => _service = service;

    [HttpGet("my-team")]
    public async Task<IActionResult> GetMyTeam(
        string memberId,
        [FromQuery] int      page     = 1,
        [FromQuery] int      pageSize = 20,
        [FromQuery] string?  search   = null,
        [FromQuery] DateTime? from    = null,
        [FromQuery] DateTime? to      = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetMyTeamAsync(memberId, page, pageSize, search, from, to, ct);
        return Ok(ApiResponse<PagedResult<EnrollmentMyTeamMemberView>>.Ok(result));
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(
        string memberId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetBranchesAsync(memberId, page, pageSize, search, ct);
        return Ok(ApiResponse<EnrollmentBranchesView>.Ok(result));
    }

    [HttpGet("branches/{branchMemberId}/detail")]
    public async Task<IActionResult> GetBranchDetail(
        string memberId, string branchMemberId, CancellationToken ct = default)
    {
        var detail = await _service.GetBranchDetailAsync(branchMemberId, ct);
        if (detail is null)
            return NotFound(ApiResponse<BranchDetailView>.Fail("NOT_FOUND", "Branch not found."));
        return Ok(ApiResponse<BranchDetailView>.Ok(detail));
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers(
        string memberId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetCustomersAsync(memberId, page, pageSize, search, ct);
        return Ok(ApiResponse<PagedResult<EnrollmentCustomerView>>.Ok(result));
    }

    [HttpGet("visualizer/stats")]
    public async Task<IActionResult> GetVisualizerStats(string memberId, CancellationToken ct = default)
    {
        var result = await _service.GetVisualizerStatsAsync(memberId, ct);
        return Ok(ApiResponse<EnrollmentVisualizerStatsView>.Ok(result));
    }

    [HttpGet("visualizer/children/{parentMemberId}")]
    public async Task<IActionResult> GetVisualizerChildren(
        string memberId, string parentMemberId, CancellationToken ct = default)
    {
        var result = await _service.GetVisualizerChildrenAsync(parentMemberId, ct);
        return Ok(ApiResponse<IEnumerable<EnrollmentVisualizerChildView>>.Ok(result));
    }
}
