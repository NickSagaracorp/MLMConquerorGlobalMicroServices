using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;

public class UpdateMemberStatusHandler : IRequestHandler<UpdateMemberStatusCommand, Result<AdminMemberDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICacheService _cache;

    public UpdateMemberStatusHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        ICacheService cache)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _cache = cache;
    }

    public async Task<Result<AdminMemberDto>> Handle(
        UpdateMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == request.MemberId, cancellationToken);

        if (member is null)
            return Result<AdminMemberDto>.Failure("MEMBER_NOT_FOUND", $"Member '{request.MemberId}' not found.");

        var oldStatus = member.Status;
        var now = _dateTime.Now;

        member.Status = request.Request.Status;
        member.LastUpdateDate = now;
        member.LastUpdateBy = _currentUser.UserId;

        var history = new MemberStatusHistory
        {
            MemberId = member.MemberId,
            OldStatus = oldStatus,
            NewStatus = request.Request.Status,
            Reason = request.Request.Reason,
            ChangedAt = now,
            CreationDate = now,
            CreatedBy = _currentUser.UserId
        };

        await _db.MemberStatusHistories.AddAsync(history, cancellationToken);

        // When an admin activates a member, sync the pending membership subscription.
        if (request.Request.Status == MemberAccountStatus.Active)
        {
            var subscription = await _db.MembershipSubscriptions
                .Where(s => s.MemberId == member.MemberId && s.SubscriptionStatus == MembershipStatus.Pending)
                .OrderByDescending(s => s.CreationDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is not null)
            {
                var startDate = subscription.StartDate == default ? now : subscription.StartDate;
                subscription.SubscriptionStatus = MembershipStatus.Active;
                subscription.StartDate          = startDate;
                subscription.EndDate            = startDate.AddMonths(1);
                subscription.RenewalDate        = startDate.AddMonths(1);
                subscription.LastUpdateDate     = now;
                subscription.LastUpdateBy       = _currentUser.UserId;
            }

            // Complete the most recent pending order so qualification points are counted.
            var pendingOrder = await _db.Orders
                .Where(o => o.MemberId == member.MemberId && o.Status == OrderStatus.Pending)
                .OrderByDescending(o => o.CreationDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (pendingOrder is not null)
            {
                pendingOrder.Status          = OrderStatus.Completed;
                pendingOrder.OrderDate       = pendingOrder.OrderDate == default ? now : pendingOrder.OrderDate;
                pendingOrder.LastUpdateDate  = now;
                pendingOrder.LastUpdateBy    = _currentUser.UserId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Cache invalidation — the member's profile snapshot in BizCenter and
        // any admin/CEO dashboards that aggregated by status are stale now.
        // We can't pattern-delete the admin:members:p{x}:s{y}:f{z} list keys
        // without Redis SCAN, so those rely on their 2-minute TTL. Cleared:
        await _cache.RemoveAsync(CacheKeys.MemberProfile(member.MemberId), cancellationToken);
        await _cache.RemoveAsync(CacheKeys.AdminCeoDashboard, cancellationToken);
        await _cache.RemoveAsync(CacheKeys.AdminFinancialDashboard, cancellationToken);
        await _cache.RemoveAsync(CacheKeys.AdminGrowthDashboard, cancellationToken);
        await _cache.RemoveAsync(CacheKeys.AdminHealthDashboard, cancellationToken);

        var dto = new AdminMemberDto
        {
            MemberId = member.MemberId,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Phone = member.Phone,
            Country = member.Country,
            Status = member.Status.ToString(),
            MemberType = member.MemberType.ToString(),
            EnrollDate = member.EnrollDate,
            SponsorMemberId = member.SponsorMemberId,
            CreationDate = member.CreationDate
        };

        return Result<AdminMemberDto>.Success(dto);
    }
}
