using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
using MLMConquerorGlobalEdition.RankEngine.Mappings;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IEmailService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;

public class EvaluateRankHandler : IRequestHandler<EvaluateRankCommand, Result<RankEvaluationResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;
    private readonly GetRankProgressHandler _progressHelper;
    private readonly ICacheService _cache;
    private readonly IPushNotificationService _push;
    private readonly IEmailService _email;
    private readonly ISender _mediator;

    public EvaluateRankHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ICurrentUserService currentUser,
        GetRankProgressHandler progressHelper,
        ICacheService cache,
        IPushNotificationService push,
        IEmailService email,
        ISender mediator)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
        _progressHelper = progressHelper;
        _cache = cache;
        _push = push;
        _email = email;
        _mediator = mediator;
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

        // Compute current metrics once
        var metrics = await _progressHelper.ComputeMetricsAsync(command.MemberId, ct);

        // Find the highest rank the member qualifies for
        RankDefinition? highestQualifiedRank = null;
        foreach (var rank in candidateRanks)
        {
            if (rank.Requirements.Count == 0) continue;
            var requirement = rank.Requirements.OrderBy(r => r.LevelNo).First();
            if (MeetsRequirements(metrics, requirement))
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

        // Notify member of rank achievement
        _ = _push.SendAsync(
            command.MemberId,
            NotificationEvents.RankAchieved,
            "Rank Achieved!",
            $"Congratulations! You have achieved the '{highestQualifiedRank.Name}' rank.",
            ct);

        // Auto-generate achievement certificate (fire-and-forget; failure is non-blocking)
        _ = _mediator.Send(new GenerateCertificateCommand(rankHistory.Id), ct);

        // Send congratulatory email to the ambassador
        var memberFullName = $"{member.FirstName} {member.LastName}".Trim();
        _ = _email.SendAsync(
            member.Email,
            memberFullName,
            "en",
            NotificationEvents.RankAchieved,
            new Dictionary<string, string>
            {
                ["FullName"]   = memberFullName,
                ["RankName"]   = highestQualifiedRank.Name,
                ["AchievedAt"] = now.ToString("MMMM dd, yyyy")
            },
            ct);

        // Notify all unique uplines (enrollment tree + dual team — deduplicated)
        await NotifyUplines(command.MemberId, highestQualifiedRank.Name, ct);

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
    /// Collects all ancestor MemberIds from both the enrollment tree and dual-team tree,
    /// deduplicates them, and sends one UplineRankAchieved push notification per unique upline.
    /// </summary>
    private async Task NotifyUplines(string memberId, string rankName, CancellationToken ct)
    {
        var genealogyNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId && !g.IsDeleted, ct);

        var dualTeamNode = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId && !d.IsDeleted, ct);

        var genealogyUplines = ParseAncestors(genealogyNode?.HierarchyPath, memberId);
        var dualTeamUplines  = ParseAncestors(dualTeamNode?.HierarchyPath, memberId);

        var allUplines = genealogyUplines.Union(dualTeamUplines).ToList();

        foreach (var uplineMemberId in allUplines)
        {
            _ = _push.SendAsync(
                uplineMemberId,
                NotificationEvents.UplineRankAchieved,
                "Team Member Rank Achievement!",
                $"A member in your team has achieved the '{rankName}' rank.",
                ct);
        }
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

    private static bool MeetsRequirements(RankMetricsResponse metrics, RankRequirement req)
    {
        if (metrics.PersonalPoints < req.PersonalPoints) return false;
        if (metrics.QualifyingTeamPoints < req.TeamPoints) return false;
        if (metrics.EnrollmentTeamCount < req.EnrollmentTeam) return false;
        if (metrics.PlacementQualifiedTeamMembers < req.PlacementQualifiedTeamMembers) return false;
        if (metrics.EnrollmentQualifiedTeamMembers < req.EnrollmentQualifiedTeamMembers) return false;
        if (metrics.SponsoredMembers < req.SponsoredMembers) return false;
        if (metrics.ExternalMembers < req.ExternalMembers) return false;
        if (metrics.SalesVolume < req.SalesVolume) return false;
        return true;
    }
}
