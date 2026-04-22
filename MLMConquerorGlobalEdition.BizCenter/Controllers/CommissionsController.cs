using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusMemberSummary;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusWeekStats;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusAmbassadors;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusBranch;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusStats;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsBreakdown;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsHistory;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsMonthBreakdown;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsSummary;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetDualResidualCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusSummary;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusSummary;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/commissions")]
[Authorize]
public class CommissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommissionsController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/bizcenter/commissions — paged list with optional status/date filters</summary>
    [HttpGet]
    public async Task<IActionResult> GetCommissions(
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 20,
        [FromQuery] string?   status   = null,
        [FromQuery] DateTime? from     = null,
        [FromQuery] DateTime? to       = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsQuery(page, pageSize, status, from, to), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/summary — pending, paid, and current-year totals</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CommissionSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CommissionSummaryDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/history — year/month grouped totals (paid only)</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsHistoryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<List<CommissionHistoryYearDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<List<CommissionHistoryYearDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/breakdown?paymentDate=&amp;earnedDate= — type breakdown. earnedDate required for pending (narrows by batch), omit for paid.</summary>
    [HttpGet("breakdown")]
    public async Task<IActionResult> GetBreakdown(
        [FromQuery] DateTime  paymentDate,
        [FromQuery] DateTime? earnedDate = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsBreakdownQuery(paymentDate, earnedDate), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<List<CommissionBreakdownDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<List<CommissionBreakdownDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/month-breakdown?year=&amp;month= — monthly detail per type</summary>
    [HttpGet("month-breakdown")]
    public async Task<IActionResult> GetMonthBreakdown([FromQuery] int year, [FromQuery] int month, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsMonthBreakdownQuery(year, month), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<List<CommissionMonthBreakdownDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<List<CommissionMonthBreakdownDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/dual-residual</summary>
    [HttpGet("dual-residual")]
    public async Task<IActionResult> GetDualResidual([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDualResidualCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/fast-start-bonus — paged earnings</summary>
    [HttpGet("fast-start-bonus")]
    public async Task<IActionResult> GetFastStartBonus(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFastStartBonusCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/fast-start-bonus/summary</summary>
    [HttpGet("fast-start-bonus/summary")]
    public async Task<IActionResult> GetFastStartBonusSummary(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFastStartBonusSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CommissionBonusSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CommissionBonusSummaryDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/boost-bonus — paged earnings</summary>
    [HttpGet("boost-bonus")]
    public async Task<IActionResult> GetBoostBonus(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBoostBonusCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/boost-bonus/week-stats</summary>
    [HttpGet("boost-bonus/week-stats")]
    public async Task<IActionResult> GetBoostBonusWeekStats(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBoostBonusWeekStatsQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<BoostBonusWeekStatsDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<BoostBonusWeekStatsDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/boost-bonus/summary</summary>
    [HttpGet("boost-bonus/summary")]
    public async Task<IActionResult> GetBoostBonusMemberSummary(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBoostBonusMemberSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<BoostBonusMemberSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<BoostBonusMemberSummaryDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/presidential-bonus — paged earnings</summary>
    [HttpGet("presidential-bonus")]
    public async Task<IActionResult> GetPresidentialBonus(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPresidentialBonusCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/presidential-bonus/summary</summary>
    [HttpGet("presidential-bonus/summary")]
    public async Task<IActionResult> GetPresidentialBonusSummary(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPresidentialBonusSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CommissionBonusSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CommissionBonusSummaryDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/car-bonus/stats — current month progress</summary>
    [HttpGet("car-bonus/stats")]
    public async Task<IActionResult> GetCarBonusStats(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCarBonusStatsQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CarBonusStatsDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CarBonusStatsDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/car-bonus/ambassadors — downline breakdown</summary>
    [HttpGet("car-bonus/ambassadors")]
    public async Task<IActionResult> GetCarBonusAmbassadors(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCarBonusAmbassadorsQuery(from, to), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<List<CarBonusAmbassadorDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<List<CarBonusAmbassadorDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/car-bonus/ambassadors/{memberId}/branch — branch member breakdown</summary>
    [HttpGet("car-bonus/ambassadors/{memberId}/branch")]
    public async Task<IActionResult> GetCarBonusBranch(
        [FromRoute] string memberId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCarBonusBranchQuery(memberId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CarBonusBranchDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CarBonusBranchDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/car-bonus — paged earnings</summary>
    [HttpGet("car-bonus")]
    public async Task<IActionResult> GetCarBonus(
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 20,
        [FromQuery] DateTime? from     = null,
        [FromQuery] DateTime? to       = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCarBonusCommissionsQuery(page, pageSize, from, to), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }
}
