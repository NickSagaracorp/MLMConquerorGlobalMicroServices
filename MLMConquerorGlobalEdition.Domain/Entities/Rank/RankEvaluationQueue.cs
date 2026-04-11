using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Rank;

/// <summary>
/// Coordination table between enrollment/placement events (SignupAPI) and
/// the rank evaluation processor (RankEngine).
///
/// Real-time flow:
///   SignupAPI inserts entries for every upline affected by an enrollment or placement.
///   ProcessRankQueueJob (every 5 min) picks them up and calls EvaluateRankHandler.
///
/// Safety-net flow:
///   RankEvaluationSweepJob (nightly 3:30 AM UTC) reprocesses any unprocessed/failed
///   entries and then does a full sweep of all active ambassadors.
/// </summary>
public class RankEvaluationQueue : AuditChangesLongKey
{
    /// <summary>The member whose action (enrollment/placement) triggered the evaluation.</summary>
    public string TriggerMemberId { get; set; } = string.Empty;

    /// <summary>The upline member whose rank should be recalculated.</summary>
    public string EvaluateMemberId { get; set; } = string.Empty;

    public RankEvaluationTrigger TriggerEvent { get; set; }

    public DateTime TriggerDate { get; set; }

    public bool IsProcessed { get; set; } = false;

    public DateTime? ProcessedAt { get; set; }

    /// <summary>Name of the job instance that processed this entry.</summary>
    public string? ProcessedBy { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Incremented on each failed evaluation attempt. Entries with RetryCount ≥ 3 are skipped by the queue job and handled by the nightly sweep.</summary>
    public int RetryCount { get; set; } = 0;
}
