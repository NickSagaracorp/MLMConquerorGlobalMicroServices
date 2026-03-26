using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Features.Charge;
using MLMConquerorGlobalEdition.Billing.Features.GetGateways;
using MLMConquerorGlobalEdition.Billing.Features.Payout;
using MLMConquerorGlobalEdition.Billing.Features.Refund;
using MLMConquerorGlobalEdition.Billing.Features.RenewMembership;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Controllers;

[ApiController]
[Route("api/v1/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IMediator _mediator;

    public BillingController(IMediator mediator)
        => _mediator = mediator;

    /// <summary>
    /// Charge a member via the specified payment gateway.
    /// Creates an Order and PaymentHistory record.
    /// For eWallet: also deducts from the member's internal balance.
    /// </summary>
    [HttpPost("charge")]
    [ProducesResponseType(typeof(ApiResponse<ChargeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Charge([FromBody] ChargeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChargeCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<ChargeResponse>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<ChargeResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Refund a previously captured payment.
    /// Supported gateways: Stripe (Dwolla) and eWallet.
    /// </summary>
    [HttpPost("refund")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refund([FromBody] RefundRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefundCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<bool>.Ok(result.Value));
    }

    /// <summary>
    /// List all available payment gateways registered in the system.
    /// </summary>
    [HttpGet("gateways")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<GatewayInfoDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGateways(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGatewaysQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<GatewayInfoDto>>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<IEnumerable<GatewayInfoDto>>.Ok(result.Value!));
    }

    /// <summary>
    /// Renew a member's membership subscription.
    /// Charges via the member's preferred wallet gateway.
    /// </summary>
    [HttpPost("memberships/renew")]
    [ProducesResponseType(typeof(ApiResponse<ChargeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RenewMembership([FromBody] MembershipRenewalRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RenewMembershipCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<ChargeResponse>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<ChargeResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Process commission payout for a member.
    /// Pays out all Pending CommissionEarnings where PaymentDate &lt;= today,
    /// crediting the member's eWallet internal balance.
    /// </summary>
    [HttpPost("wallets/payout")]
    [ProducesResponseType(typeof(ApiResponse<PayoutResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Payout([FromBody] PayoutRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new PayoutCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PayoutResponse>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<PayoutResponse>.Ok(result.Value!));
    }
}
