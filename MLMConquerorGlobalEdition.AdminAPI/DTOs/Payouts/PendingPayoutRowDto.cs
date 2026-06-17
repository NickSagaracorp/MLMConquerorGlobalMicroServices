using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;

public class PendingPayoutRowDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public decimal PendingAmount { get; set; }
    public WalletType WalletType { get; set; }
    public string? LastAttemptOutcome { get; set; }
    public string? LastAttemptError { get; set; }
}
