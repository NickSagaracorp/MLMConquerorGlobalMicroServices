using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>Los pagos de cuota que ocupan un ciclo (Plan K, K10). La misma regla que UX_AnnualFeePayments_LivePerCycle.</summary>
public static class AnnualFeePaymentQueries
{
    public static IQueryable<AnnualFeePayment> LiveForCycle(AppDbContext db, string billingStateId, DateTime cycleDue)
    {
        var cycle = cycleDue.Date;
        return db.AnnualFeePayments.Where(p =>
            p.SubscriptionBillingStateId == billingStateId
            && p.CycleDueDate == cycle
            && (p.Status == AnnualFeePaymentStatus.Pending
                || p.Status == AnnualFeePaymentStatus.Completed
                || p.Status == AnnualFeePaymentStatus.NeedsReconciliation));
    }
}
