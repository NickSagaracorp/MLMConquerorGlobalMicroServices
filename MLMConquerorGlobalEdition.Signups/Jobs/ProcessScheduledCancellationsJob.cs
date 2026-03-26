using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Signups.Services;

namespace MLMConquerorGlobalEdition.Signups.Jobs;

/// <summary>
/// Nightly HangFire job (suggested schedule: 0 1 * * *  — 1:00 AM UTC).
/// Finds subscriptions with a future CancellationDate that has now been reached
/// and finalises the cancellation: flips status to Cancelled, marks the member
/// Inactive, and reverses all MemberStatisticEntity values up the sponsor chain.
/// </summary>
public class ProcessScheduledCancellationsJob
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ISponsorBonusService _sponsorBonus;

    public ProcessScheduledCancellationsJob(
        AppDbContext db, IDateTimeProvider dateTime, ISponsorBonusService sponsorBonus)
    {
        _db = db;
        _dateTime = dateTime;
        _sponsorBonus = sponsorBonus;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var today = _dateTime.Now.Date;

        // Find subscriptions whose scheduled cancellation date has arrived
        // but whose status has not yet been flipped to Cancelled.
        var due = await _db.MembershipSubscriptions
            .Where(s => s.CancellationDate.HasValue
                     && s.CancellationDate.Value.Date <= today
                     && s.SubscriptionStatus != MembershipStatus.Cancelled
                     && s.SubscriptionStatus != MembershipStatus.Expired)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        var memberIds = due.Select(s => s.MemberId).Distinct().ToList();
        var now = _dateTime.Now;

        // Bulk-load everything needed to avoid N+1 queries
        var members = await _db.MemberProfiles
            .Where(m => memberIds.Contains(m.MemberId))
            .ToListAsync(ct);

        var genealogyNodes = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => memberIds.Contains(g.MemberId))
            .ToListAsync(ct);

        var memberStats = await _db.MemberStatistics
            .Where(s => memberIds.Contains(s.MemberId))
            .ToListAsync(ct);

        // Collect all ancestor IDs across all cancelled members to load in one shot
        var allAncestorIds = genealogyNodes
            .SelectMany(g => g.HierarchyPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(id => id != g.MemberId))
            .Distinct()
            .ToList();

        var allAncestorStats = allAncestorIds.Count > 0
            ? await _db.MemberStatistics
                .Where(s => allAncestorIds.Contains(s.MemberId))
                .ToListAsync(ct)
            : new List<Domain.Entities.Member.MemberStatisticEntity>();

        var ancestorStatsDict = allAncestorStats.ToDictionary(s => s.MemberId);
        var membersDict = members.ToDictionary(m => m.MemberId);
        var memberStatsDict = memberStats.ToDictionary(s => s.MemberId);
        var genealogyDict = genealogyNodes.ToDictionary(g => g.MemberId);

        var statusHistories = new List<MemberStatusHistory>();

        foreach (var subscription in due)
        {
            var memberId = subscription.MemberId;

            // Flip subscription
            subscription.SubscriptionStatus = MembershipStatus.Cancelled;
            subscription.EndDate = now;
            subscription.IsAutoRenew = false;
            subscription.LastUpdateBy = "system";

            // Flip member account status
            if (membersDict.TryGetValue(memberId, out var member)
                && member.Status != MemberAccountStatus.Inactive)
            {
                var prev = member.Status;
                member.Status = MemberAccountStatus.Inactive;
                member.LastUpdateDate = now;

                statusHistories.Add(new MemberStatusHistory
                {
                    MemberId = memberId,
                    OldStatus = prev,
                    NewStatus = MemberAccountStatus.Inactive,
                    Reason = "Scheduled membership cancellation processed.",
                    ChangedAt = now,
                    CreatedBy = "system",
                    CreationDate = now
                });
            }

            // Reverse stat propagation
            var pointsToRemove = 0;
            if (memberStatsDict.TryGetValue(memberId, out var memberStat))
            {
                pointsToRemove = memberStat.PersonalPoints;
                memberStat.PersonalPoints = 0;
            }

            if (!genealogyDict.TryGetValue(memberId, out var genealogyNode)) continue;

            var uplineIds = genealogyNode.HierarchyPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(id => id != memberId)
                .ToList();

            foreach (var uplineId in uplineIds)
            {
                if (!ancestorStatsDict.TryGetValue(uplineId, out var uplineStat)) continue;

                uplineStat.EnrollmentPoints = Math.Max(0, uplineStat.EnrollmentPoints - pointsToRemove);
                uplineStat.EnrollmentTeamSize = Math.Max(0, uplineStat.EnrollmentTeamSize - 1);

                if (uplineId == genealogyNode.ParentMemberId)
                    uplineStat.QualifiedSponsoredMembers = Math.Max(0, uplineStat.QualifiedSponsoredMembers - 1);
            }
        }

        if (statusHistories.Count > 0)
            await _db.MemberStatusHistories.AddRangeAsync(statusHistories, ct);

        // ── Reverse sponsor bonuses for signups within the 14-day window ──────
        // Load signup subscriptions for all cancelled members
        var signupSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => memberIds.Contains(s.MemberId)
                     && s.ChangeReason == SubscriptionChangeReason.New
                     && s.LastOrderId != null)
            .ToListAsync(ct);

        var signupOrderIds = signupSubs
            .Select(s => s.LastOrderId!)
            .Distinct()
            .ToList();

        var signupOrders = signupOrderIds.Count > 0
            ? await _db.Orders
                .AsNoTracking()
                .Where(o => signupOrderIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, ct)
            : new Dictionary<string, Domain.Entities.Orders.Orders>();

        foreach (var sub in signupSubs)
        {
            if (sub.LastOrderId is null) continue;
            if (!signupOrders.TryGetValue(sub.LastOrderId, out var signupOrder)) continue;
            if ((now - signupOrder.OrderDate).TotalDays > 14) continue;

            await _sponsorBonus.TryReverseAsync(
                sub.MemberId, sub.LastOrderId,
                "Scheduled cancellation within 14-day window.",
                now, "system", ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
