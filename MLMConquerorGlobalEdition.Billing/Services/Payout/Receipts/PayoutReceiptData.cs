using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public record ReceiptEarningLine(string CommissionEarningId, decimal Amount);

public record PayoutReceiptData(
    long PayoutAttemptId,
    string MemberId,
    string FullName,
    WalletType WalletType,
    string PayoutAccountSnapshot,
    decimal AmountUsd,
    DateTime ProcessDateUtc,
    DateTime CompletedAtUtc,
    string? GatewayTransactionId,
    IReadOnlyList<ReceiptEarningLine> Earnings);
