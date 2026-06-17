using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Groups one CSV bulk-payout export (Sprint 19 uses it; created here so the bulk path
/// needs no second migration). A batch reserves earnings on export and is reconciled later.
/// </summary>
public class PayoutBatch : AuditChangesStringKey
{
    public WalletType WalletType { get; set; }
    public DateTime ProcessDateUtc { get; set; }

    /// <summary>One of <see cref="PayoutBatchStatus"/>.</summary>
    public string Status { get; set; } = PayoutBatchStatus.Exported;

    public string? ExportCsvUrl { get; set; }
    public string? ResultCsvUrl { get; set; }
    public int MemberCount { get; set; }
    public decimal TotalAmountUsd { get; set; }
    public string? ReconciledBy { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public string? Notes { get; set; }
}
