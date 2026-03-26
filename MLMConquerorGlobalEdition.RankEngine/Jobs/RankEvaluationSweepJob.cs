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
/// Evaluates rank progress for all active ambassadors and promotes where criteria are met.
/// Idempotent: EvaluateRankHandler is safe to re-run per member.
/// </summary>
public class RankEvaluationSweepJob
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;
    private readonly ILogger<RankEvaluationSweepJob> _logger;

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
        _logger.LogInformation("RankEvaluationSweepJob: starting at {Now}.", DateTime.UtcNow);

        var memberIds = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.Status == MemberAccountStatus.Active
                     && m.MemberType == MemberType.Ambassador)
            .Select(m => m.MemberId)
            .ToListAsync(ct);

        _logger.LogInformation(
            "RankEvaluationSweepJob: evaluating {Count} active ambassadors.", memberIds.Count);

        var promoted = 0;
        var failed   = 0;

        foreach (var memberId in memberIds)
        {
            try
            {
                var result = await _mediator.Send(new EvaluateRankCommand(memberId), ct);

                if (result.IsSuccess)
                    promoted++;
                else
                    _logger.LogDebug(
                        "RankEvaluationSweepJob: no promotion for {MemberId} [{Code}]: {Message}",
                        memberId, result.ErrorCode, result.Error);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "RankEvaluationSweepJob: unhandled error evaluating member {MemberId}.", memberId);
            }
        }

        _logger.LogInformation(
            "RankEvaluationSweepJob: completed — {Promoted} evaluated, {Failed} errors.",
            promoted, failed);
    }
}
