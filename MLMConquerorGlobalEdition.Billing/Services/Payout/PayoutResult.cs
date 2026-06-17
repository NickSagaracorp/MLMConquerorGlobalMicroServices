using MLMConquerorGlobalEdition.Domain.Constants;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

public class PayoutResult
{
    public string MemberId { get; init; } = string.Empty;
    public string Outcome { get; init; } = PayoutOutcome.Pending;
    public decimal AmountUsd { get; init; }
    public int EarningsCount { get; init; }
    public long PayoutAttemptId { get; init; }
    public string? GatewayErrorCode { get; init; }
    public string? GatewayErrorMessage { get; init; }
}
