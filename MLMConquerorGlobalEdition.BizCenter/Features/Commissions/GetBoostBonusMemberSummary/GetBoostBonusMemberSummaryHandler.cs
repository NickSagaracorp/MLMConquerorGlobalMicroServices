using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusMemberSummary;

public class GetBoostBonusMemberSummaryHandler : IRequestHandler<GetBoostBonusMemberSummaryQuery, Result<BoostBonusMemberSummaryDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetBoostBonusMemberSummaryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<BoostBonusMemberSummaryDto>> Handle(GetBoostBonusMemberSummaryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Downline member IDs from enrollment tree (direct and indirect)
        var downlineMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains($"/{memberId}/") && g.MemberId != memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (downlineMemberIds.Count == 0)
            return Result<BoostBonusMemberSummaryDto>.Success(new BoostBonusMemberSummaryDto());

        // Latest subscription per downline member
        var subscriptions = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => downlineMemberIds.Contains(s.MemberId) && !s.IsDeleted)
            .GroupBy(s => s.MemberId)
            .Select(g => g.OrderByDescending(s => s.CreationDate).First())
            .ToListAsync(ct);

        var total    = subscriptions.Count;
        var active   = subscriptions.Count(s => s.SubscriptionStatus == MembershipStatus.Active);
        var inactive = total - active;

        var dto = new BoostBonusMemberSummaryDto
        {
            TotalMembers      = total,
            ActiveRebilling   = active,
            InactiveRebilling = inactive
        };

        return Result<BoostBonusMemberSummaryDto>.Success(dto);
    }
}
