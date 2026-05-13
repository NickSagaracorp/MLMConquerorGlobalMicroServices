using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Commission;

/// <summary>
/// New source-of-truth ledger for Daily Residual (binary) accruals.
/// The CommissionEngine's daily-residual calculation writes here instead of CommissionEarning
/// for all new accruals. Existing CommissionEarning rows are untouched.
///
/// When the consolidation job (weekly Mondays) or an ad-hoc commission-balance payment runs,
/// pending rows whose sum >= DailyResidualConsolidationMinimum are marked Paid and a single
/// consolidated CommissionEarning credit row is created (ConsolidatedIntoCommissionEarningId).
/// </summary>
public class DailyResidualEarning : AuditChangesLongKey
{
    public string BeneficiaryMemberId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime EarnedDate { get; set; }
    public CommissionEarningStatus Status { get; set; } = CommissionEarningStatus.Pending;

    /// <summary>Order that triggered this residual (binary qualification period), if applicable.</summary>
    public string? SourceOrderId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Set when this row has been consolidated into a CommissionEarning credit row.
    /// Points to the CommissionEarning.Id that represents the aggregated payout.
    /// </summary>
    public string? ConsolidatedIntoCommissionEarningId { get; set; }

    // ── Point-in-time snapshot fields (set at calculation time; null for backfilled legacy rows) ──

    /// <summary>
    /// The member's current RankDefinition.Id at the moment this earning was calculated.
    /// Nullable FK-ish reference to RankDefinition; no enforced FK constraint (snapshot, not live relation).
    /// Null for rows backfilled from legacy CommissionEarning data.
    /// </summary>
    public int? CurrentRankId { get; set; }

    /// <summary>
    /// The capped/eligible dual-team points that qualified this member for their tier on this day.
    /// Matches MemberStatisticEntity.DualTeamPoints (type int) — the raw value used by the
    /// daily-residual handler's tier-qualification query. Zero for enrollment-based tier winners.
    /// Null for backfilled legacy rows.
    /// </summary>
    public int? EligibleDualTeamPoints { get; set; }

    /// <summary>
    /// The capped/eligible enrollment-team points that qualified this member for their tier on this day.
    /// Matches MemberStatisticEntity.EnrollmentPoints (type int). Zero for DT-based tier winners.
    /// Null for backfilled legacy rows.
    /// </summary>
    public int? EligibleEnrollmentTeamPoints { get; set; }

    /// <summary>
    /// The member's personal points (MemberStatisticEntity.PersonalPoints) at the time of calculation.
    /// Null for backfilled legacy rows.
    /// </summary>
    public int? PersonalPoints { get; set; }

    // ── Payment-tracking fields (set when Status transitions to Paid) ──────────

    /// <summary>
    /// The UTC timestamp at which this row was marked Paid (i.e., consolidated into a CommissionEarning credit).
    /// Null while still Pending.
    /// </summary>
    public DateTime? PaymentDate { get; set; }

    /// <summary>
    /// The actor or system process that recorded the consolidation/payment.
    /// Examples: "weekly-consolidation" (Monday consolidation job), a member-id or
    /// "membership-token-purchase" (ad-hoc consolidation to fund a recurring renewal).
    /// Null while still Pending.
    /// </summary>
    public string? CommentedBy { get; set; }

    /// <summary>
    /// A human-readable description of the consolidation event (separate from <see cref="Notes"/>,
    /// which is set at accrual time by the daily-residual calculation handler).
    /// Examples:
    ///   "Consolidated into CommissionEarning #abc123 by the weekly daily-residual consolidation job"
    ///   "Consolidated into CommissionEarning #abc123 to fund token #tok456 for renewal order #ord789"
    /// Null while still Pending.
    /// </summary>
    public string? PaymentComment { get; set; }
}
