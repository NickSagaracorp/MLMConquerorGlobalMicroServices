using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
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
    private readonly IBackgroundJobClient _jobs;

    public EvaluateRankHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ICurrentUserService currentUser,
        IRankQualificationService qualification,
        ICacheService cache,
        IBackgroundJobClient jobs)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
        _qualification = qualification;
        _cache = cache;
        _jobs = jobs;
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

        // Batched qualification: load member inputs ONCE, evaluate every candidate rank's
        // primary requirement in-memory. Each rank's "primary" requirement is the lowest
        // LevelNo row (kept aligned with the previous foreach loop semantics).
        // Ranks with no requirements rows are skipped exactly as before.
        var primaryRequirements = candidateRanks
            .Where(r => r.Requirements.Count > 0)
            .Select(r => r.Requirements.OrderBy(rr => rr.LevelNo).First())
            .ToList();

        var qualificationResults = await _qualification.QualifiesForAllRanksAsync(
            command.MemberId, primaryRequirements, ct);

        // Project results back onto candidate ranks so we know per-rank qualification.
        var qualifiesByRankId = qualificationResults
            .ToDictionary(r => r.Requirement.RankDefinitionId, r => r.Result.Qualifies);

        // Highest qualifying rank: walk ranks ascending and pick the largest SortOrder that qualifies.
        // We intentionally do NOT stop at the first non-qualifying rank — qualification is
        // not strictly monotonic across rank rows (a higher rank can opt-out of an axis the
        // member fails on a lower rank). Picking the maximum qualifying SortOrder mirrors
        // the original foreach behavior.
        RankDefinition? highestQualifiedRank = null;
        foreach (var rank in candidateRanks)
        {
            if (rank.Requirements.Count == 0) continue;
            if (qualifiesByRankId.TryGetValue(rank.Id, out var ok) && ok)
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

        // ── Skip-rank: persist EVERY qualifying intermediate rank between current and
        //    the highest qualifying rank as its own MemberRankHistory row. Walking
        //    ranks in ascending SortOrder lets us chain PreviousRankId so the history
        //    tells the full promotion story even when a member jumps multiple ranks
        //    in one evaluation. Certificates are NOT generated here — they are minted
        //    on demand when the member or admin actually requests one.
        var now = _dateTime.Now;
        var createdBy = _currentUser.UserId;

        var ranksToRecord = candidateRanks
            .Where(r => r.Requirements.Count > 0
                        && r.SortOrder > currentSortOrder
                        && r.SortOrder <= highestQualifiedRank.SortOrder
                        && qualifiesByRankId.TryGetValue(r.Id, out var ok) && ok)
            .OrderBy(r => r.SortOrder)
            .ToList();

        int? previousRankId = currentRankHistory?.RankDefinitionId;
        MemberRankHistory? topHistoryRow = null;
        // A multi-rank climb is recognized in a single evaluation instant, but recording
        // every intermediate rank with the SAME AchievedAt produces physically impossible
        // history ("achieved two ranks at the exact same second"). Stamp each successive
        // rank with a monotonic +1s offset (ranksToRecord is already ordered by SortOrder)
        // so the achievements are strictly increasing and distinct, matching the order in
        // which the member crossed each threshold. The offset is intentionally tiny — it
        // reflects the real promotion sequence, not a fabricated multi-minute gap.
        var rankOffset = 0;
        foreach (var rank in ranksToRecord)
        {
            var achievedAt = now.AddSeconds(rankOffset);
            var row = new MemberRankHistory
            {
                MemberId = command.MemberId,
                RankDefinitionId = rank.Id,
                PreviousRankId = previousRankId,
                AchievedAt = achievedAt,
                CreatedBy = createdBy,
                CreationDate = achievedAt,
                LastUpdateDate = achievedAt
            };
            await _db.MemberRankHistories.AddAsync(row, ct);
            previousRankId = rank.Id;
            topHistoryRow = row;
            rankOffset++;
        }

        // Defensive fallback: if for any reason no intermediate row qualified (e.g., the
        // highest rank's requirements pass but a lower rank's don't because of an opt-out
        // pattern), still record the headline promotion so the system stays in a consistent
        // state. This mirrors the legacy single-row behavior for that edge case.
        if (topHistoryRow is null)
        {
            topHistoryRow = new MemberRankHistory
            {
                MemberId = command.MemberId,
                RankDefinitionId = highestQualifiedRank.Id,
                PreviousRankId = currentRankHistory?.RankDefinitionId,
                AchievedAt = now,
                CreatedBy = createdBy,
                CreationDate = now,
                LastUpdateDate = now
            };
            await _db.MemberRankHistories.AddAsync(topHistoryRow, ct);
        }

        await _db.SaveChangesAsync(ct);

        // Invalidate rank cache for this member
        await _cache.RemoveAsync(CacheKeys.MemberRank(command.MemberId), ct);

        // ── Notifications: fire ONLY for the headline rank (the highest reached in this
        //    evaluation). One promotion event per evaluation — not one per intermediate row.
        //    Push / email / upline notifications run as Hangfire jobs on their own DI scopes
        //    so a notification I/O hiccup never blocks the evaluation path.
        _jobs.Enqueue<RankNotificationJobs>(j =>
            j.NotifyRankAchievedAsync(command.MemberId, highestQualifiedRank.Name));

        _jobs.Enqueue<RankNotificationJobs>(j =>
            j.SendRankAchievedEmailAsync(command.MemberId, highestQualifiedRank.Name, now));

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
