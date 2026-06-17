using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;

public class PayoutBatchRowDto
{
    public string Id { get; set; } = string.Empty;
    public WalletType WalletType { get; set; }
    public DateTime ProcessDateUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public decimal TotalAmountUsd { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime? ReconciledAt { get; set; }
}
