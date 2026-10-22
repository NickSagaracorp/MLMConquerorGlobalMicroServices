using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Loyalty;

/// <summary>Puntos de lealtad de un miembro, por páginas y con los totales de toda su historia.</summary>
public interface IMemberLoyaltyService
{
    /// <summary>Página de <paramref name="memberId"/>. El BizCenter pasa el miembro del token; el Admin, el de la ruta.</summary>
    Task<LoyaltyPointsPageView> GetPageAsync(string memberId, int page, int pageSize, CancellationToken ct = default);
}

public sealed class LoyaltyPointView
{
    public string    Id                      { get; set; } = string.Empty;
    public string    ProductId               { get; set; } = string.Empty;
    public string    OrderId                 { get; set; } = string.Empty;
    public decimal   PointsEarned            { get; set; }
    public bool      IsLocked                { get; set; }
    public bool      MissedPayment           { get; set; }
    public int       NumberOfSuccessPayments { get; set; }
    public int       MonthNo                 { get; set; }
    public int       YearNo                  { get; set; }
    public DateTime? UnlockedAt              { get; set; }
}

/// <summary>La página y, aparte, los totales de TODA la historia (no solo de la página).</summary>
public sealed class LoyaltyPointsPageView : PagedResult<LoyaltyPointView>
{
    public decimal TotalPoints    { get; set; }
    public decimal UnlockedPoints { get; set; }
    public decimal LockedPoints   { get; set; }
}

public sealed class MemberLoyaltyService : IMemberLoyaltyService
{
    private readonly AppDbContext _db;

    public MemberLoyaltyService(AppDbContext db) => _db = db;

    public async Task<LoyaltyPointsPageView> GetPageAsync(string memberId, int page, int pageSize, CancellationToken ct = default)
    {
        var all = _db.LoyaltyPoints.AsNoTracking().Where(lp => lp.MemberId == memberId);

        var totalCount = await all.CountAsync(ct);
        var locked     = await all.Where(lp => lp.IsLocked).SumAsync(lp => (decimal?)lp.PointsEarned, ct) ?? 0m;
        var unlocked   = await all.Where(lp => !lp.IsLocked).SumAsync(lp => (decimal?)lp.PointsEarned, ct) ?? 0m;

        var items = await all
            .OrderByDescending(lp => lp.YearNo)
            .ThenByDescending(lp => lp.MonthNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(lp => new LoyaltyPointView
            {
                Id                      = lp.Id,
                ProductId               = lp.ProductId,
                OrderId                 = lp.OrderId,
                PointsEarned            = lp.PointsEarned,
                IsLocked                = lp.IsLocked,
                MissedPayment           = lp.MissedPayment,
                NumberOfSuccessPayments = lp.NumberOfSuccessPayments,
                MonthNo                 = lp.MonthNo,
                YearNo                  = lp.YearNo,
                UnlockedAt              = lp.UnlockedAt
            })
            .ToListAsync(ct);

        return new LoyaltyPointsPageView
        {
            Items          = items,
            TotalCount     = totalCount,
            Page           = page,
            PageSize       = pageSize,
            TotalPoints    = locked + unlocked,
            UnlockedPoints = unlocked,
            LockedPoints   = locked
        };
    }
}
