using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;

public class PayoutBatchMemberDto
{
    public long PayoutAttemptId { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public decimal AmountUsd { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? GatewayErrorCode { get; set; }
    public string? GatewayErrorMessage { get; set; }
    public string? GatewayTransactionId { get; set; }
}

public class PayoutBatchDetailDto
{
    public string Id { get; set; } = string.Empty;
    public WalletType WalletType { get; set; }
    public DateTime ProcessDateUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public decimal TotalAmountUsd { get; set; }
    public string? ExportCsvUrl { get; set; }
    public string? ResultCsvUrl { get; set; }
    public string? ReconciledBy { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreationDate { get; set; }
    public List<PayoutBatchMemberDto> Members { get; set; } = new();
}
