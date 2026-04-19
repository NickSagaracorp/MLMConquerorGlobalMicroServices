using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.BackfillFsbCountdowns;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CancelCommission;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CreateCommission;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetAdminCommissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.PayCommissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminCommissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminCommissionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("commissions")]
    public async Task<IActionResult> GetCommissions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetAdminCommissionsQuery(new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<AdminCommissionDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<AdminCommissionDto>>.Ok(result.Value!));
    }

    [HttpPost("commissions")]
    public async Task<IActionResult> CreateCommission(
        [FromBody] CreateCommissionRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateCommissionCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminCommissionDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminCommissionDto>.Ok(result.Value!));
    }

    [HttpDelete("commissions/{commissionId}")]
    public async Task<IActionResult> CancelCommission(string commissionId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelCommissionCommand(commissionId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Commission cancelled."));
    }

    [HttpPost("commissions/pay")]
    public async Task<IActionResult> PayCommissions(
        [FromBody] PayCommissionsRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new PayCommissionsCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<int>.Ok(result.Value!, $"{result.Value} commission(s) marked as paid."));
    }

    [HttpPost("commissions/import-csv")]
    public IActionResult ImportCsv()
    {
        return StatusCode(
            StatusCodes.Status501NotImplemented,
            ApiResponse<object>.Fail("NOT_IMPLEMENTED", "CSV import is not yet implemented in this iteration."));
    }

    /// <summary>
    /// POST /api/v1/admin/commissions/fsb-countdown/backfill
    /// Creates FSB countdown records for all ambassadors that don't have one yet.
    /// Safe to re-run — skips ambassadors that already have a record.
    /// </summary>
    [HttpPost("commissions/fsb-countdown/backfill")]
    public async Task<IActionResult> BackfillFsbCountdowns(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new BackfillFsbCountdownsCommand(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<BackfillFsbCountdownsResult>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<BackfillFsbCountdownsResult>.Ok(
            result.Value!,
            $"{result.Value!.Created} countdown record(s) created."));
    }
}
