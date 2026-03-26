using MediatR;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.ProcessRefundWebhook;

/// <summary>
/// Command dispatched when a payment provider posts a refund event to
/// POST /api/v1/webhooks/{provider}/refund.
/// </summary>
/// <param name="Provider">Provider name from the URL path (e.g. "stripe", "braintree", "crypto").</param>
/// <param name="Payload">Deserialized inbound payload from the provider.</param>
public record ProcessRefundWebhookCommand(
    string Provider,
    WebhookPaymentPayload Payload) : IRequest<Result<bool>>;
