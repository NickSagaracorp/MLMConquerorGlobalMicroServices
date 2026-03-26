using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoStats;

public class GetPromoStatsHandler : IRequestHandler<GetPromoStatsQuery, Result<PromoStatsDto>>
{
    private readonly AppDbContext _db;

    public GetPromoStatsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PromoStatsDto>> Handle(
        GetPromoStatsQuery request, CancellationToken cancellationToken)
    {
        var promo = await _db.CorporatePromos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PromoId && !x.IsDeleted, cancellationToken);

        if (promo is null)
            return Result<PromoStatsDto>.Failure("PROMO_NOT_FOUND", "Corporate promo not found.");

        // Subscriptions created during promo window (one per member — take the first)
        var promoSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => s.CreationDate >= promo.StartDate && s.CreationDate <= promo.EndDate)
            .Include(s => s.MembershipLevel)
            .ToListAsync(cancellationToken);

        var uniqueMemberIds = promoSubs.Select(s => s.MemberId).Distinct().ToList();
        var totalSignups = uniqueMemberIds.Count;

        var activeSubscriptions = promoSubs
            .Where(s => s.SubscriptionStatus == MembershipStatus.Active)
            .Select(s => s.MemberId)
            .Distinct()
            .Count();

        var cancelledSubscriptions = promoSubs
            .Where(s => s.SubscriptionStatus == MembershipStatus.Cancelled)
            .Select(s => s.MemberId)
            .Distinct()
            .Count();

        var retentionRate = totalSignups > 0
            ? Math.Round((decimal)activeSubscriptions / totalSignups * 100, 2)
            : 0m;

        var churnRate = totalSignups > 0
            ? Math.Round((decimal)cancelledSubscriptions / totalSignups * 100, 2)
            : 0m;

        // Total income: orders placed during promo window
        var totalIncome = await _db.Orders
            .AsNoTracking()
            .Where(o => o.OrderDate >= promo.StartDate && o.OrderDate <= promo.EndDate)
            .SumAsync(o => o.TotalAmount, cancellationToken);

        // Total commissions earned during promo window
        var totalCommissions = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.EarnedDate >= promo.StartDate && c.EarnedDate <= promo.EndDate
                        && c.Status == CommissionEarningStatus.Paid)
            .SumAsync(c => c.Amount, cancellationToken);

        // Signups grouped by membership level
        var signupsByLevel = promoSubs
            .Where(s => s.MembershipLevel != null)
            .GroupBy(s => s.MembershipLevel!.Name)
            .ToDictionary(g => g.Key, g => g.Select(s => s.MemberId).Distinct().Count());

        return Result<PromoStatsDto>.Success(new PromoStatsDto
        {
            TotalSignups = totalSignups,
            ActiveSubscriptions = activeSubscriptions,
            CancelledSubscriptions = cancelledSubscriptions,
            RetentionRate = retentionRate,
            ChurnRate = churnRate,
            TotalIncome = totalIncome,
            TotalCommissions = totalCommissions,
            SignupsByMembershipLevel = signupsByLevel
        });
    }
}
