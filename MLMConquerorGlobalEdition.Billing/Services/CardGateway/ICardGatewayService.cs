using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

public class GatewayChargeRequest
{
    public string  MemberId             { get; init; } = string.Empty;
    public decimal Amount               { get; init; }
    public string  Currency             { get; init; } = "USD";
    public string  Description          { get; init; } = string.Empty;
    public string? TokenizedCardRef     { get; init; }
    public string? NetworkTransactionId { get; init; }
    public bool    IsRecurring          { get; init; }
}

public class GatewayChargeResult
{
    public string  GatewayTransactionId { get; init; } = string.Empty;
    public string  Status               { get; init; } = string.Empty;
    public string? RawResponse          { get; init; }
}

/// <summary>
/// Card-charge gateway abstraction (separate from the wallet/payout IGatewayService).
/// </summary>
public interface ICardGatewayService
{
    CardProcessor Processor { get; }

    Task<Result<GatewayChargeResult>> ChargeAsync(
        GatewayChargeRequest req,
        CancellationToken ct = default);

    Task<Result<bool>> RefundAsync(
        string gatewayTransactionId,
        decimal amount,
        CancellationToken ct = default);
}
