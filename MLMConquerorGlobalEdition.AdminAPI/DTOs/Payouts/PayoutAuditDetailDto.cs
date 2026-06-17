using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;

public class PayoutAuditEarningDto
{
    public string CommissionEarningId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class PayoutAuditDetailDto
{
    public long PayoutAttemptId { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public WalletType WalletTypeSnapshot { get; set; }
    public string PayoutAccountSnapshot { get; set; } = string.Empty;
    public string? PayoutAccountMetaSnapshot { get; set; }
    public decimal AmountUsd { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public DateTime ProcessDateUtc { get; set; }
    public DateTime AttemptedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? GatewayErrorCode { get; set; }
    public string? GatewayErrorMessage { get; set; }
    public string DisbursementMode { get; set; } = string.Empty;
    public string? ReceiptUrl { get; set; }
    public string? ReceiptSha256 { get; set; }
    public long? ReceiptLedgerSeq { get; set; }
    public string? ReceiptAnchorRef { get; set; }
    public List<PayoutAuditEarningDto> Earnings { get; set; } = new();
}
