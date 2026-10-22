using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Loyalty;

public class LoyaltyPointsService : ILoyaltyPointsService
{
    private readonly AppDbContext _db;

    public LoyaltyPointsService(AppDbContext db) => _db = db;

    public async Task<LoyaltyPointsPageView> GetPageAsync(string memberId, int page, int pageSize, CancellationToken ct = default)
    {
        var mine = _db.LoyaltyPoints.AsNoTracking().Where(lp => lp.MemberId == memberId);

        var totalCount = await mine.CountAsync(ct);

        // Los totales son de todos los registros del miembro (un registro por mes y producto: pocos), no solo de la pagina.
        var points = await mine.Select(lp => new { lp.PointsEarned, lp.IsLocked }).ToListAsync(ct);

        var items = await mine
            .OrderByDescending(lp => lp.YearNo)
            .ThenByDescending(lp => lp.MonthNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(lp => new LoyaltyPointView
            {
                Id = lp.Id,
                ProductId = lp.ProductId,
                OrderId = lp.OrderId,
                PointsEarned = lp.PointsEarned,
                IsLocked = lp.IsLocked,
                MissedPayment = lp.MissedPayment,
                NumberOfSuccessPayments = lp.NumberOfSuccessPayments,
                MonthNo = lp.MonthNo,
                YearNo = lp.YearNo,
                UnlockedAt = lp.UnlockedAt
            })
            .ToListAsync(ct);

        return new LoyaltyPointsPageView
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPoints = points.Sum(p => p.PointsEarned),
            UnlockedPoints = points.Where(p => !p.IsLocked).Sum(p => p.PointsEarned),
            LockedPoints = points.Where(p => p.IsLocked).Sum(p => p.PointsEarned)
        };
    }
}
