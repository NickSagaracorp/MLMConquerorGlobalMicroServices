using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateBoostBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateDailyResidual;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateFastStartBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateMatchingBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculatePresidentialBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateSponsorBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Features.GetCommissionCollection;
using MLMConquerorGlobalEdition.CommissionEngine.Features.GetCommissionRules;
using MLMConquerorGlobalEdition.CommissionEngine.Features.ReverseSponsorBonus;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Controllers;

[ApiController]
[Route("api/v1/commissions")]
[Authorize]
public class CommissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommissionsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Returns all active commission types with their configured rules and thresholds.
    /// </summary>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(ApiResponse<List<CommissionTypeResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCommissionRulesQuery(), ct);
        return Ok(ApiResponse<List<CommissionTypeResponse>>.Ok(result.Value!));
    }

    /// <summary>
    /// Returns paginated commission earnings for the given period.
    /// collectionId: "YYYY-MM-DD" (exact date) or "YYYY-MM" (full month).
    /// </summary>
    [HttpGet("collection/{collectionId}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CommissionEarningResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCollection(
        string collectionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionCollectionQuery(collectionId, page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningResponse>>.Ok(result.Value!));
    }

    /// <summary>
    /// Triggers Fast Start Bonus calculation for a specific completed order.
    /// Called in real-time when an order completes (or by admin for backfill).
    /// </summary>
    [HttpPost("calculate/fast-start-bonus")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<CalculationResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculateFastStartBonus(
        [FromBody] FastStartBonusRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CalculateFastStartBonusCommand(request.OrderId, request.BuyerMemberId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CalculationResultResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Runs the nightly binary residual calculation.
    /// Normally triggered by HangFire; also callable by admin for manual runs.
    /// </summary>
    [HttpPost("calculate/daily-residual")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<CalculationResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculateDailyResidual(
        [FromQuery] DateTime? periodDate, CancellationToken ct)
    {
        var result = await _mediator.Send(new CalculateDailyResidualCommand(periodDate), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CalculationResultResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Runs the weekly Boost Bonus calculation.
    /// Normally triggered by HangFire every Sunday; also callable by admin.
    /// </summary>
    [HttpPost("calculate/boost-bonus")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<CalculationResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculateBoostBonus(
        [FromQuery] DateTime? periodDate, CancellationToken ct)
    {
        var result = await _mediator.Send(new CalculateBoostBonusCommand(periodDate), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CalculationResultResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Runs the monthly Presidential Bonus calculation.
    /// Normally triggered by HangFire on the 1st of each month; also callable by admin.
    /// </summary>
    [HttpPost("calculate/presidential-bonus")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<CalculationResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculatePresidentialBonus(
        [FromQuery] DateTime? periodDate, CancellationToken ct)
    {
        var result = await _mediator.Send(new CalculatePresidentialBonusCommand(periodDate), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CalculationResultResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Runs the Matching Bonus calculation for a given period.
    /// Must be run after Daily Residual has been calculated for the same period.
    /// </summary>
    [HttpPost("calculate/matching-bonus")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<CalculationResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculateMatchingBonus(
        [FromQuery] DateTime? periodDate, CancellationToken ct)
    {
        var result = await _mediator.Send(new CalculateMatchingBonusCommand(periodDate), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CalculationResultResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Calculates the sponsor bonus for a specific signup order.
    /// Triggered automatically on signup completion; also callable by admin for backfill.
    /// </summary>
    [HttpPost("calculate/sponsor-bonus")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<CalculationResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculateSponsorBonus(
        [FromBody] SponsorBonusRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CalculateSponsorBonusCommand(request.NewMemberId, request.OrderId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CalculationResultResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Reverses a sponsor bonus when a member cancels within the 14-day chargeback window.
    /// Pending → cancelled in place. Paid → negative reversal earning via CommissionType.ReverseId.
    /// </summary>
    [HttpPost("reverse/sponsor-bonus")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<CalculationResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReverseSponsorBonus(
        [FromBody] ReverseSponsorBonusRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ReverseSponsorBonusCommand(request.CancelledMemberId, request.SignupOrderId, request.Reason), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CalculationResultResponse>.Ok(result.Value!));
    }
}

// Request body DTOs (controller-only, never exposed in Domain)
public record FastStartBonusRequest(string OrderId, string BuyerMemberId);
public record SponsorBonusRequest(string NewMemberId, string OrderId);
public record ReverseSponsorBonusRequest(string CancelledMemberId, string SignupOrderId, string? Reason);
