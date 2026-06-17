using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// One high-level record per payment attempt to one ambassador. Immutable once written
/// (snapshot of gateway + payout account is frozen at payment time). This is the
/// anti-dispute record: it proves which gateway and which exact account received the
/// grouped earnings, regardless of later wallet changes.
/// </summary>
public class PayoutAttempt : AuditChangesLongKey
{
    public string MemberId { get; set; } = string.Empty;

    // Frozen at payment time — never updated after creation.
    public WalletType WalletTypeSnapshot { get; set; }
    public string PayoutAccountSnapshot { get; set; } = string.Empty;
    public string? PayoutAccountMetaSnapshot { get; set; } // JSON: crypto network, currency, country, etc.

    public decimal AmountUsd { get; set; }
    public DateTime ProcessDateUtc { get; set; }

    /// <summary>One of <see cref="PayoutOutcome"/>: Pending | Success | Failed.</summary>
    public string Outcome { get; set; } = PayoutOutcome.Pending;

    public string? GatewayTransactionId { get; set; }
    public string? GatewayErrorCode { get; set; }
    public string? GatewayErrorMessage { get; set; }

    public DateTime AttemptedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public long? LatencyMs { get; set; }

    public int EarningsCount { get; set; }

    public DisbursementMode DisbursementMode { get; set; } = DisbursementMode.Online;
    public string? PayoutBatchId { get; set; } // null for Online; set for CsvBulk

    // Receipt fields — populated in Sprint 18, nullable until then.
    public string? ReceiptUrl { get; set; }
    public string? ReceiptSha256 { get; set; }
    public long? ReceiptLedgerSeq { get; set; }
    public string? ReceiptPrevHash { get; set; }
    public string? ReceiptAnchorRef { get; set; }

    // Mutable update tracking (outcome transitions: Pending → Success | Failed).
    public DateTime? LastUpdateDate { get; set; }
    public string? LastUpdateBy { get; set; }
}
