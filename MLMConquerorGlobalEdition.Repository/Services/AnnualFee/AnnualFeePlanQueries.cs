using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>Lo que cuesta la cuota y a qué producto se imputa.</summary>
public sealed record AnnualFeePrice(decimal Amount, string ProductId);

/// <summary>El plan de la cuota anual y su precio (Plan K, K2). Un solo sitio para Billing y el BizCenter.</summary>
public static class AnnualFeePlanQueries
{
    /// <summary>El plan marcado como de la cuota, activo, con sus productos. Null si no hay.</summary>
    public static Task<RecurringBillingPlan?> ActivePlanAsync(AppDbContext db, CancellationToken ct = default) =>
        db.RecurringBillingPlans
          .Include(p => p.PlanProducts)
          .FirstOrDefaultAsync(p => p.IsAnnualFeePlan && p.IsActive, ct);

    /// <summary>
    /// El importe: FixedAmountOverride del plan o, si no, el AnnualPrice de su producto (99 en local).
    /// Null si no hay producto o el importe no es positivo. Exige PlanProducts cargado.
    /// </summary>
    public static async Task<AnnualFeePrice?> PriceAsync(AppDbContext db, RecurringBillingPlan plan, CancellationToken ct = default)
    {
        var productId = plan.PlanProducts.OrderBy(pp => pp.Id).Select(pp => pp.ProductId).FirstOrDefault();
        if (productId is null) return null;

        var annualPrice = await db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => (decimal?)p.AnnualPrice)
            .FirstOrDefaultAsync(ct);

        var amount = plan.FixedAmountOverride ?? annualPrice ?? 0m;
        return amount > 0 ? new AnnualFeePrice(amount, productId) : null;
    }
}
