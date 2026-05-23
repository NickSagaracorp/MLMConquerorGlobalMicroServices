using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.RankEngine.Jobs;

/// <summary>
/// Hangfire jobs that fan out the side-effect notifications fired by a rank achievement
/// (member push, congratulations email, upline pushes).
///
/// EvaluateRankHandler enqueues these instead of running them in-line, so:
///   • Rank evaluation completes fast (no I/O coupled to notification latency).
///   • A signup burst → many queue entries → ProcessRankQueueJob can evaluate them quickly,
///     and the notification work fans out across the 5 Hangfire workers in parallel.
///   • A notification failure (Firebase down, SMTP down) cannot stall the rank queue —
///     it gets its own retry / dead-letter lifecycle via Hangfire.
///   • Each job invocation runs in its OWN DI scope (own AppDbContext), so no concurrent
///     evaluation can race another on a shared DbContext.
///
/// Methods carry [Queue("rank")] so they run on the RankEngine Hangfire server only —
/// no sibling service can steal them (per the per-service-queue isolation rule).
/// </summary>
public class RankNotificationJobs
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationService _push;
    private readonly IEmailService _email;
    private readonly ILogger<RankNotificationJobs> _logger;

    public RankNotificationJobs(
        AppDbContext db,
        IPushNotificationService push,
        IEmailService email,
        ILogger<RankNotificationJobs> logger)
    {
        _db     = db;
        _push   = push;
        _email  = email;
        _logger = logger;
    }

    /// <summary>Pushes the "you achieved a rank" notification to the achieving member.</summary>
    [Queue("rank")]
    public async Task NotifyRankAchievedAsync(string memberId, string rankName)
    {
        try
        {
            await _push.SendAsync(
                memberId,
                NotificationEvents.RankAchieved,
                "Rank Achieved!",
                $"Congratulations! You have achieved the '{rankName}' rank.",
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RankAchieved push failed for {MemberId} (rank '{Rank}').", memberId, rankName);
            throw; // let Hangfire retry per its configured policy
        }
    }

    /// <summary>Pushes the "a team member achieved a rank" notification to one upline.</summary>
    [Queue("rank")]
    public async Task NotifyUplineRankAchievedAsync(string uplineMemberId, string rankName)
    {
        try
        {
            await _push.SendAsync(
                uplineMemberId,
                NotificationEvents.UplineRankAchieved,
                "Team Member Rank Achievement!",
                $"A member in your team has achieved the '{rankName}' rank.",
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "UplineRankAchieved push failed for {UplineMemberId} (rank '{Rank}').", uplineMemberId, rankName);
            throw;
        }
    }

    /// <summary>Sends the congratulations email to the achieving member.</summary>
    [Queue("rank")]
    public async Task SendRankAchievedEmailAsync(string memberId, string rankName, DateTime achievedAt)
    {
        try
        {
            var member = await _db.MemberProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MemberId == memberId);

            if (member is null)
            {
                _logger.LogWarning("RankAchieved email skipped — member {MemberId} not found.", memberId);
                return;
            }

            var fullName = $"{member.FirstName} {member.LastName}".Trim();
            await _email.SendAsync(
                member.Email,
                fullName,
                member.DefaultLanguage,
                NotificationEvents.RankAchieved,
                new Dictionary<string, string>
                {
                    ["FullName"]   = fullName,
                    ["RankName"]   = rankName,
                    ["AchievedAt"] = achievedAt.ToString("MMMM dd, yyyy")
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RankAchieved email failed for {MemberId} (rank '{Rank}').", memberId, rankName);
            throw;
        }
    }
}
