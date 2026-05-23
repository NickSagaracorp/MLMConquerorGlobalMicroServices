using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;
using MLMConquerorGlobalEdition.RankEngine.Jobs;
using MLMConquerorGlobalEdition.RankEngine.Mappings;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;

public class EvaluateRankHandler : IRequestHandler<EvaluateRankCommand, Result<RankEvaluationResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;
    private readonly IRankQualificationService _qualification;
    private readonly ICacheService _cache;
    private readonly ISender _mediator;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<EvaluateRankHandler> _logger;

    public EvaluateRankHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ICurrentUserService currentUser,
        IRankQualificationService qualification,
        ICacheService cache,
        ISender mediator,
        IBackgroundJobClient jobs,
        ILogger<EvaluateRankHandler> logger)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
        _qualification = qualification;
        _cache = cache;
        _mediator = mediator;
        _jobs = jobs;
        _logger = logger;
    }

    public async Task<Result<RankEvaluationResponse>> Handle(EvaluateRankCommand command, CancellationToken ct)
    {
        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == command.MemberId, ct);

        if (member is null)
            return Result<RankEvaluationResponse>.Failure("MEMBER_NOT_FOUND", $"Member '{command.MemberId}' not found.");

        // Current highest rank achieved
        var currentRankHistory = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(h => h.RankDefinition)
            .Where(h => h.MemberId == command.MemberId && !h.IsDeleted)
            .OrderByDescending(h => h.RankDefinition!.SortOrder)
            .FirstOrDefaultAsync(ct);

        var currentSortOrder = currentRankHistory?.RankDefinition?.SortOrder ?? 0;

        // All active ranks above current, ordered ascending (evaluate from next to highest)
        var candidateRanks = await _db.RankDefinitions
            .AsNoTracking()
            .Include(r => r.Requirements)
            .Where(r => r.Status == RankDefinitionStatus.Active && r.SortOrder > currentSortOrder)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        if (candidateRanks.Count == 0)
        {
            return Result<RankEvaluationResponse>.Success(new RankEvaluationResponse
            {
                MemberId = command.MemberId,
                RankAchieved = false,
                AchievedRank = currentRankHistory?.RankDefinition is not null
                    ? RankEngineMappingExtensions.ToResponse(currentRankHistory.RankDefinition)
                    : null,
                Message = "Member is already at the highest rank or no higher ranks are available.",
                EvaluatedAt = _dateTime.Now
            });
        }

        // Find the highest rank the member qualifies for — via the single authority.
        RankDefinition? highestQualifiedRank = null;
        foreach (var rank in candidateRanks)
        {
            if (rank.Requirements.Count == 0) continue;
            var requirement = rank.Requirements.OrderBy(r => r.LevelNo).First();
            var result = await _qualification.QualifiesForRankAsync(command.MemberId, requirement, ct);
            if (result.Qualifies)
                highestQualifiedRank = rank;
        }

        if (highestQualifiedRank is null)
        {
            return Result<RankEvaluationResponse>.Success(new RankEvaluationResponse
            {
                MemberId = command.MemberId,
                RankAchieved = false,
                AchievedRank = currentRankHistory?.RankDefinition is not null
                    ? RankEngineMappingExtensions.ToResponse(currentRankHistory.RankDefinition)
                    : null,
                Message = "Member does not qualify for a rank advancement at this time.",
                EvaluatedAt = _dateTime.Now
            });
        }

        // Record the rank achievement
        var now = _dateTime.Now;
        var rankHistory = new MemberRankHistory
        {
            MemberId = command.MemberId,
            RankDefinitionId = highestQualifiedRank.Id,
            PreviousRankId = currentRankHistory?.RankDefinitionId,
            AchievedAt = now,
            CreatedBy = _currentUser.UserId,
            CreationDate = now,
            LastUpdateDate = now
        };

        await _db.MemberRankHistories.AddAsync(rankHistory, ct);
        await _db.SaveChangesAsync(ct);

        // Invalidate rank cache for this member
        await _cache.RemoveAsync(CacheKeys.MemberRank(command.MemberId), ct);

        // Generate the achievement certificate synchronously: it shares this scope's
        // AppDbContext (safe because awaited sequentially), and the cert is the user-
        // visible deliverable of a promotion — we want it on disk before we return.
        // A cert failure is logged but never aborts the promotion (admin can regenerate).
        try
        {
            var certResult = await _mediator.Send(new GenerateCertificateCommand(rankHistory.Id), ct);
            if (!certResult.IsSuccess)
                _logger.LogWarning(
                    "Certificate generation failed for member {MemberId}, rank '{Rank}': {Code} — {Error}",
                    command.MemberId, highestQualifiedRank.Name, certResult.ErrorCode, certResult.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Certificate generation threw for member {MemberId}, rank '{Rank}'.",
                command.MemberId, highestQualifiedRank.Name);
        }

        // ── Notifications: decoupled via Hangfire ────────────────────────────────────
        // Push / email / upline notifications are enqueued as separate Hangfire jobs
        // (RankNotificationJobs). Each job runs on its OWN DI scope with its OWN
        // AppDbContext and gets Hangfire's retry / durability for free. This keeps the
        // evaluation path fast under signup-burst load: dozens of simultaneous signups
        // produce dozens of queue entries → ProcessRankQueueJob churns through them
        // quickly because each EvaluateRank call no longer waits on notification I/O.

        _jobs.Enqueue<RankNotificationJobs>(j =>
            j.NotifyRankAchievedAsync(command.MemberId, highestQualifiedRank.Name));

        _jobs.Enqueue<RankNotificationJobs>(j =>
            j.SendRankAchievedEmailAsync(command.MemberId, highestQualifiedRank.Name, now));

        // Upline notifications — we still compute the upline set here (one DB read on
        // this scope, safe), then enqueue one job per unique upline so the fan-out
        // happens across Hangfire workers, not serially in this handler.
        var uplines = await ComputeAllUplinesAsync(command.MemberId, ct);
        foreach (var uplineMemberId in uplines)
        {
            _jobs.Enqueue<RankNotificationJobs>(j =>
                j.NotifyUplineRankAchievedAsync(uplineMemberId, highestQualifiedRank.Name));
        }

        return Result<RankEvaluationResponse>.Success(new RankEvaluationResponse
        {
            MemberId = command.MemberId,
            RankAchieved = true,
            AchievedRank = RankEngineMappingExtensions.ToResponse(highestQualifiedRank),
            PreviousRank = currentRankHistory?.RankDefinition is not null
                ? RankEngineMappingExtensions.ToResponse(currentRankHistory.RankDefinition)
                : null,
            Message = $"Congratulations! Member has achieved the '{highestQualifiedRank.Name}' rank.",
            EvaluatedAt = now
        });
    }

    /// <summary>
    /// Collects all unique ancestor MemberIds from both the enrollment tree and the
    /// dual-team tree, deduplicated. Pure computation — no notifications fired here.
    /// </summary>
    private async Task<List<string>> ComputeAllUplinesAsync(string memberId, CancellationToken ct)
    {
        var genealogyNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId && !g.IsDeleted, ct);

        var dualTeamNode = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId && !d.IsDeleted, ct);

        var genealogyUplines = ParseAncestors(genealogyNode?.HierarchyPath, memberId);
        var dualTeamUplines  = ParseAncestors(dualTeamNode?.HierarchyPath, memberId);

        return genealogyUplines.Union(dualTeamUplines).ToList();
    }

    /// <summary>
    /// Parses a materialized HierarchyPath ("/AMB-001/AMB-002/AMB-003/")
    /// and returns all ancestor MemberIds, excluding the member themselves.
    /// </summary>
    private static IEnumerable<string> ParseAncestors(string? hierarchyPath, string selfMemberId)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPath))
            return Enumerable.Empty<string>();

        return hierarchyPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => id != selfMemberId);
    }
}
