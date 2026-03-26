using MediatR;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.ProcessPaymentWebhook;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.ProcessRefundWebhook;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Controllers;

/// <summary>
/// Receives inbound webhook events from payment providers.
///
/// Endpoints are intentionally public (no [Authorize]) — providers post raw payloads
/// without JWT tokens. Production hardening (HMAC signature validation per provider)
/// should be added inside each handler or as a dedicated filter.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;

    public WebhooksController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Receives a payment event (e.g. payment.success / payment.failed) from a provider.
    /// </summary>
    /// <param name="provider">Provider slug in the URL, e.g. "stripe", "braintree", "crypto".</param>
    /// <param name="payload">Normalised webhook payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{provider}/payment")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PaymentWebhook(
        [FromRoute] string provider,
        [FromBody]  WebhookPaymentPayload payload,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ProcessPaymentWebhookCommand(provider, payload), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<bool>.Ok(true, "Webhook processed successfully."));
    }

    /// <summary>
    /// Receives a refund event from a provider and marks the payment + order as refunded.
    /// </summary>
    /// <param name="provider">Provider slug in the URL, e.g. "stripe", "braintree", "crypto".</param>
    /// <param name="payload">Normalised webhook payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{provider}/refund")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefundWebhook(
        [FromRoute] string provider,
        [FromBody]  WebhookPaymentPayload payload,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ProcessRefundWebhookCommand(provider, payload), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<bool>.Ok(true, "Refund webhook processed successfully."));
    }
}
