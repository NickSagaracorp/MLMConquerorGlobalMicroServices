using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Immutable join of which CommissionEarning rows entered a given payout attempt.
/// Also reserves an earning while an attempt is non-failed, preventing double-payment
/// across online and CSV-bulk modes.
/// </summary>
public class PayoutAttemptEarning : AuditChangesLongKey
{
    public long PayoutAttemptId { get; set; }
    public string CommissionEarningId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
