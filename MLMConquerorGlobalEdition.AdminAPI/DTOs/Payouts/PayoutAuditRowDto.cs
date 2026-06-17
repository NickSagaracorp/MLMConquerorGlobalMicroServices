using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;

public class PayoutAuditRowDto
{
    public long PayoutAttemptId { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public WalletType WalletTypeSnapshot { get; set; }
    public string PayoutAccountSnapshot { get; set; } = string.Empty;
    public decimal AmountUsd { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public DateTime ProcessDateUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? GatewayErrorCode { get; set; }
    public bool HasReceipt { get; set; }
    public bool Anchored { get; set; }
}
