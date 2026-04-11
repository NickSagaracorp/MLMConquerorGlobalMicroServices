using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;

namespace MLMConquerorGlobalEdition.RankEngine.Jobs;

/// <summary>
/// HangFire recurring job — every 5 minutes.
///
/// Picks up unprocessed <see cref="Domain.Entities.Rank.RankEvaluationQueue"/> entries written
/// by SignupAPI during enrollments and placements, deduplicates by target member, evaluates rank
/// for each unique upline, and marks entries as processed.
///
/// Entries that fail 3 times are left for the nightly <see cref="RankEvaluationSweepJob"/>.
/// </summary>
public class ProcessRankQueueJob
{
    private const int MaxRetries   = 3;
    private const int BatchSize    = 200;
    private const string JobSource = "ProcessRankQueueJob";

    private readonly IMediator                      _mediator;
    private readonly AppDbContext                   _db;
    private readonly ILogger<ProcessRankQueueJob>   _logger;

    public ProcessRankQueueJob(
        IMediator mediator,
        AppDbContext db,
        ILogger<ProcessRankQueueJob> logger)
    {
        _mediator = mediator;
        _db       = db;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // Load the next batch: pending, retriable, oldest first
        var entries = await _db.RankEvaluationQueue
            .Where(e => !e.IsProcessed && e.RetryCount < MaxRetries)
            .OrderBy(e => e.TriggerDate)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (entries.Count == 0)
            return;

        _logger.LogInformation(
            "{Job}: processing {Count} queue entries.", JobSource, entries.Count);

        // Deduplicate — evaluate each target member only once per run
        var targets = entries
            .Select(e => e.EvaluateMemberId)
            .Distinct()
            .ToList();

        var promoted = 0;
        var unchanged = 0;
        var failed = 0;

        foreach (var memberId in targets)
        {
            var memberEntries = entries
                .Where(e => e.EvaluateMemberId == memberId)
                .ToList();
            try
            {
                var result = await _mediator.Send(new EvaluateRankCommand(memberId), ct);

                var now = DateTime.UtcNow;
                foreach (var entry in memberEntries)
                {
                    entry.IsProcessed  = true;
                    entry.ProcessedAt  = now;
                    entry.ProcessedBy  = JobSource;
                    entry.ErrorMessage = null;
                }

                if (result.IsSuccess && result.Value?.RankAchieved == true)
                    promoted++;
                else
                    unchanged++;
            }
            catch (Exception ex)
            {
                failed++;
                var errorMsg = ex.Message.Length > 900
                    ? ex.Message[..900]
                    : ex.Message;

                foreach (var entry in memberEntries)
                {
                    entry.RetryCount++;
                    entry.ErrorMessage = errorMsg;
                    entry.ProcessedBy  = JobSource;
                }

                _logger.LogError(ex,
                    "{Job}: error evaluating rank for member {MemberId} (retry {Retry}/{Max}).",
                    JobSource, memberId, memberEntries.Max(e => e.RetryCount), MaxRetries);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "{Job}: done — {Promoted} promoted, {Unchanged} unchanged, {Failed} failed.",
            JobSource, promoted, unchanged, failed);
    }
}
