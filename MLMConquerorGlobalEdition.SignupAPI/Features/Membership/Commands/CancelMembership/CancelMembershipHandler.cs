using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.CancelMembership;

public class CancelMembershipHandler : IRequestHandler<CancelMembershipCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ISponsorBonusService _sponsorBonus;

    public CancelMembershipHandler(
        AppDbContext db, IDateTimeProvider dateTime, ISponsorBonusService sponsorBonus)
    {
        _db = db;
        _dateTime = dateTime;
        _sponsorBonus = sponsorBonus;
    }

    public async Task<Result<bool>> Handle(CancelMembershipCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;
        var effectiveDate = command.ScheduledCancellationDate ?? now;

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(x => x.MemberId == command.MemberId, ct);
        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", $"Member '{command.MemberId}' not found.");

        var subscription = await _db.MembershipSubscriptions
            .FirstOrDefaultAsync(x => x.MemberId == command.MemberId
                                   && x.SubscriptionStatus == MembershipStatus.Active, ct);
        if (subscription is null)
            return Result<bool>.Failure(
                "NO_ACTIVE_SUBSCRIPTION", "Member has no active membership subscription.");

        subscription.IsAutoRenew = false;
        subscription.CancellationDate = effectiveDate;
        subscription.LastUpdateBy = command.MemberId;

        if (effectiveDate.Date <= now.Date
            && subscription.SubscriptionStatus != MembershipStatus.Cancelled)
        {
            subscription.SubscriptionStatus = MembershipStatus.Cancelled;
            subscription.EndDate = now;
            subscription.LastUpdateBy = command.MemberId;

            // Mark member inactive and log the status change
            var previousStatus = member.Status;
            member.Status = MemberAccountStatus.Inactive;
            member.LastUpdateDate = now;

            await _db.MemberStatusHistories.AddAsync(new MemberStatusHistory
            {
                MemberId = command.MemberId,
                OldStatus = previousStatus,
                NewStatus = MemberAccountStatus.Inactive,
                Reason = command.Reason ?? "Membership cancelled.",
                ChangedAt = now,
                CreatedBy = command.MemberId,
                CreationDate = now
            }, ct);

            await ReverseUplineStatsAsync(command.MemberId, now, ct);

            await TryReverseSponsorBonusIfEligibleAsync(command.MemberId, command.Reason, now, ct);
        }
        // else: effective date is in the future — leave status Active.
        // ProcessScheduledCancellationsJob will finalise it when the date arrives.

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Reverses the sponsor bonus for the original signup order if cancellation falls
    /// within the 14-day chargeback protection window.
    /// </summary>
    private async Task TryReverseSponsorBonusIfEligibleAsync(
        string memberId, string? reason, DateTime now, CancellationToken ct)
    {
        // Locate the original signup subscription (ChangeReason = New) to find the signup order
        var signupSub = await _db.MembershipSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemberId == memberId
                                   && s.ChangeReason == SubscriptionChangeReason.New
                                   && s.LastOrderId != null, ct);

        if (signupSub?.LastOrderId is null) return;

        var signupOrder = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == signupSub.LastOrderId, ct);

        if (signupOrder is null) return;

        // 14-day chargeback window
        if ((now - signupOrder.OrderDate).TotalDays > 14) return;

        await _sponsorBonus.TryReverseAsync(
            memberId, signupSub.LastOrderId, reason, now, memberId, ct);
    }

    /// <summary>
    /// Subtracts the cancelled member's personal points from all ancestor stat records,
    /// decrements EnrollmentTeamSize, and zeroes out the member's own PersonalPoints.
    /// Uses Math.Max(0, …) to guard against going negative due to prior manual adjustments.
    /// </summary>
    private async Task ReverseUplineStatsAsync(
        string memberId, DateTime now, CancellationToken ct)
    {
        // Member's genealogy node — provides the full ancestor chain via HierarchyPath
        var memberNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);

        if (memberNode is null) return;

        // Member's own stat record — tells us how many points to subtract
        var memberStat = await _db.MemberStatistics
            .FirstOrDefaultAsync(s => s.MemberId == memberId, ct);

        var pointsToRemove = memberStat?.PersonalPoints ?? 0;

        if (memberStat is not null)
            memberStat.PersonalPoints = 0;

        if (pointsToRemove <= 0 && memberNode.ParentMemberId is null)
            return; // nothing to propagate

        // Extract ancestor IDs from the member's own path:
        // "/A/B/sponsorId/memberId/" → [A, B, sponsorId, memberId] → remove memberId → [A, B, sponsorId]
        var uplineIds = memberNode.HierarchyPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => id != memberId)
            .ToList();

        if (uplineIds.Count == 0) return;

        var uplineStats = await _db.MemberStatistics
            .Where(s => uplineIds.Contains(s.MemberId))
            .ToListAsync(ct);

        foreach (var stat in uplineStats)
        {
            stat.EnrollmentPoints = Math.Max(0, stat.EnrollmentPoints - pointsToRemove);
            stat.EnrollmentTeamSize = Math.Max(0, stat.EnrollmentTeamSize - 1);

            // Direct sponsor also loses one qualified sponsored member
            if (stat.MemberId == memberNode.ParentMemberId)
                stat.QualifiedSponsoredMembers = Math.Max(0, stat.QualifiedSponsoredMembers - 1);
        }
    }
}
