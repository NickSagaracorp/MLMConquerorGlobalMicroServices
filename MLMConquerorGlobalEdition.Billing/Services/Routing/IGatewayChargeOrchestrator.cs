using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

public class OrchestratorChargeRequest
{
    public string  MemberId             { get; init; } = string.Empty;
    public string? TokenizedCardRef     { get; init; }
    public string? NetworkTransactionId { get; init; }
    public string  Description          { get; init; } = string.Empty;
    public string? OrderId              { get; init; }
    public bool    IsRecurring          { get; init; }

    /// <summary>Raw card details for a first-time charge (e.g. signup) when no vaulted token exists yet.</summary>
    public RawCardDetails? RawCard { get; init; }

    /// <summary>Whether Spreedly should vault the card on a successful RawCard charge.</summary>
    public bool RetainOnSuccess { get; init; }
}

public class OrchestratorChargeResult
{
    public string  PaymentHistoryId      { get; init; } = string.Empty;
    public string  GatewayTransactionId  { get; init; } = string.Empty;
    public string  ProcessorUsed         { get; init; } = string.Empty;
    public decimal AmountCharged         { get; init; }
    public string  CurrencyCharged       { get; init; } = string.Empty;

    /// <summary>"Success" | "Scheduled" | "Failed"</summary>
    public string  Status                { get; init; } = string.Empty;
    public string? ScheduledJobId        { get; init; }

    /// <summary>Populated on success when the gateway vaulted a new payment method (see GatewayChargeResult).</summary>
    public string? SpreedlyPaymentMethodToken { get; init; }
}

public interface IGatewayChargeOrchestrator
{
    Task<Result<OrchestratorChargeResult>> ExecuteAsync(
        GatewayRoutingPlan plan,
        GatewayRoutingContext ctx,
        OrchestratorChargeRequest chargeReq,
        CancellationToken ct = default);
}
