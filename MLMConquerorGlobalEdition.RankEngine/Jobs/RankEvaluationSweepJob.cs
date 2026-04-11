using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;

namespace MLMConquerorGlobalEdition.RankEngine.Jobs;

/// <summary>
/// HangFire recurring job — Nightly 3:30 AM UTC.
///
/// Two-phase safety net:
///   Phase 1 — Re-evaluates any members with unprocessed or failed (RetryCount ≥ 3) queue entries
///             from the past 48 hours that the real-time <see cref="ProcessRankQueueJob"/> may have missed.
///   Phase 2 — Full sweep of all active ambassadors regardless of queue state.
///
/// Idempotent: EvaluateRankHandler is safe to re-run per member (only promotes when criteria are met).
/// </summary>
public class RankEvaluationSweepJob
{
    private const string JobSource = "RankEvaluationSweepJob";

    private readonly IMediator                          _mediator;
    private readonly AppDbContext                       _db;
    private readonly ILogger<RankEvaluationSweepJob>    _logger;

    public RankEvaluationSweepJob(
        IMediator mediator,
        AppDbContext db,
        ILogger<RankEvaluationSweepJob> logger)
    {
        _mediator = mediator;
        _db       = db;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("{Job}: starting at {Now}.", JobSource, DateTime.UtcNow);

        // ── Phase 1: recover missed/failed queue entries (past 48h) ──────────
        var cutoff = DateTime.UtcNow.AddHours(-48);

        var stuckMemberIds = await _db.RankEvaluationQueue
            .AsNoTracking()
            .Where(e => !e.IsProcessed && e.TriggerDate >= cutoff)
            .Select(e => e.EvaluateMemberId)
            .Distinct()
            .ToListAsync(ct);

        if (stuckMemberIds.Count > 0)
        {
            _logger.LogInformation(
                "{Job}: Phase 1 — recovering {Count} members with unprocessed queue entries.",
                JobSource, stuckMemberIds.Count);

            var phase1Promoted = 0;
            var phase1Failed = 0;

            foreach (var memberId in stuckMemberIds)
            {
                try
                {
                    var result = await _mediator.Send(new EvaluateRankCommand(memberId), ct);

                    // Mark all their pending entries as recovered by sweep
                    var pending = await _db.RankEvaluationQueue
                        .Where(e => !e.IsProcessed && e.EvaluateMemberId == memberId)
                        .ToListAsync(ct);

                    var now = DateTime.UtcNow;
                    foreach (var entry in pending)
                    {
                        entry.IsProcessed  = true;
                        entry.ProcessedAt  = now;
                        entry.ProcessedBy  = JobSource;
                        entry.ErrorMessage = null;
                    }
                    await _db.SaveChangesAsync(ct);

                    if (result.IsSuccess && result.Value?.RankAchieved == true)
                        phase1Promoted++;
                }
                catch (Exception ex)
                {
                    phase1Failed++;
                    _logger.LogError(ex,
                        "{Job}: Phase 1 error evaluating member {MemberId}.", JobSource, memberId);
                }
            }

            _logger.LogInformation(
                "{Job}: Phase 1 complete — {Promoted} promoted, {Failed} errors.",
                JobSource, phase1Promoted, phase1Failed);
        }

        // ── Phase 2: full sweep of all active ambassadors ────────────────────
        var memberIds = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.Status == MemberAccountStatus.Active
                     && m.MemberType == MemberType.Ambassador)
            .Select(m => m.MemberId)
            .ToListAsync(ct);

        _logger.LogInformation(
            "{Job}: Phase 2 — evaluating {Count} active ambassadors.", JobSource, memberIds.Count);

        var promoted = 0;
        var failed   = 0;

        foreach (var memberId in memberIds)
        {
            try
            {
                var result = await _mediator.Send(new EvaluateRankCommand(memberId), ct);

                if (result.IsSuccess && result.Value?.RankAchieved == true)
                    promoted++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "{Job}: Phase 2 error evaluating member {MemberId}.", JobSource, memberId);
            }
        }

        _logger.LogInformation(
            "{Job}: Phase 2 complete — {Promoted} promoted, {Failed} errors.",
            JobSource, promoted, failed);
    }
}
