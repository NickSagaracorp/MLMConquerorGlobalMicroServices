using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.UpgradeMembership;

public class UpgradeMembershipHandler : IRequestHandler<UpgradeMembershipCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public UpgradeMembershipHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db = db;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(UpgradeMembershipCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        var member = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.MemberId == command.MemberId, ct);
        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", $"Member '{command.MemberId}' not found.");

        var currentSub = await _db.MembershipSubscriptions
            .Include(x => x.MembershipLevel)
            .FirstOrDefaultAsync(x => x.MemberId == command.MemberId && x.SubscriptionStatus == MembershipStatus.Active, ct);
        if (currentSub is null)
            return Result<bool>.Failure("NO_ACTIVE_SUBSCRIPTION", "Member has no active membership subscription.");

        var newLevel = await _db.MembershipLevels.FirstOrDefaultAsync(x => x.Id == command.NewMembershipLevelId && x.IsActive, ct);
        if (newLevel is null)
            return Result<bool>.Failure("MEMBERSHIP_LEVEL_NOT_FOUND", "Target membership level not found or inactive.");

        // Domain rule validation — throws MembershipChangeNotAllowedException if invalid
        currentSub.ValidateChange(newLevel.SortOrder, currentSub.MembershipLevel!.SortOrder, SubscriptionChangeReason.Upgrade);

        // Close current subscription
        currentSub.SubscriptionStatus = MembershipStatus.Expired;
        currentSub.EndDate = now;
        currentSub.LastUpdateBy = command.MemberId;

        // Create new subscription
        var newSub = new MembershipSubscription
        {
            MemberId = command.MemberId,
            MembershipLevelId = command.NewMembershipLevelId,
            PreviousMembershipLevelId = currentSub.MembershipLevelId,
            ChangeReason = SubscriptionChangeReason.Upgrade,
            SubscriptionStatus = MembershipStatus.Active,
            StartDate = now,
            IsFree = newLevel.IsFree,
            IsAutoRenew = newLevel.IsAutoRenew,
            CreatedBy = command.MemberId,
            CreationDate = now,
            LastUpdateDate = now
        };

        await _db.MembershipSubscriptions.AddAsync(newSub, ct);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
