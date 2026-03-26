using MediatR;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.ProcessPaymentWebhook;

/// <summary>
/// Command dispatched when a payment provider posts a payment event to
/// POST /api/v1/webhooks/{provider}/payment.
/// </summary>
/// <param name="Provider">Provider name from the URL path (e.g. "stripe", "braintree", "crypto").</param>
/// <param name="Payload">Deserialized inbound payload from the provider.</param>
public record ProcessPaymentWebhookCommand(
    string Provider,
    WebhookPaymentPayload Payload) : IRequest<Result<bool>>;
