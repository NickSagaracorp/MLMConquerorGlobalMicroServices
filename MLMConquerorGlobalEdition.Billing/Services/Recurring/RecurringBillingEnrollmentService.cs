using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

public class RecurringBillingEnrollmentService : IRecurringBillingEnrollmentService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public RecurringBillingEnrollmentService(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db       = db;
        _dateTime = dateTime;
    }

    public async Task EnsureStateForSubscriptionAsync(
        MembershipSubscription subscription,
        string actor,
        CancellationToken ct = default)
    {
        // Look up the product linked to this subscription via the membership level
        // (subscription → MembershipLevel → Product).
        // The plan is linked to products via RecurringBillingPlanProduct.ProductId.
        var productId = await ResolveProductIdAsync(subscription, ct);
        if (productId is null)
            return; // no product linked — not a recurring-plan product

        // Find the active plan that covers this product
        var planProduct = await _db.RecurringBillingPlanProducts
            .AsNoTracking()
            .Include(pp => pp.Plan)
            .FirstOrDefaultAsync(pp => pp.ProductId == productId && pp.Plan!.IsActive, ct);

        if (planProduct?.Plan is null)
            return; // no active plan governs this product

        var plan = planProduct.Plan;
        var now  = _dateTime.Now;

        // NextBillingDate is start of the first/next cycle
        var nextBillingDate = plan.CycleType == RecurringCycleType.Every30Days
            ? subscription.StartDate.AddDays(30)
            : subscription.StartDate.AddYears(1);

        // Upsert: if a state already exists for this subscription, refresh it
        var existing = await _db.SubscriptionBillingStates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.MembershipSubscriptionId == subscription.Id, ct);

        if (existing is null)
        {
            var state = new SubscriptionBillingState
            {
                MembershipSubscriptionId = subscription.Id,
                MemberId                 = subscription.MemberId,
                RecurringBillingPlanId   = plan.Id,
                BillingAnchorDate        = subscription.StartDate,
                LastSuccessfulBillingDate = null,
                NextBillingDate          = nextBillingDate,
                NextAttemptDate          = nextBillingDate,
                CurrentAttemptIndex      = 0,
                Status                   = RecurringBillingStatus.Active,
                CreatedBy                = actor,
                CreationDate             = now,
                LastUpdateDate           = now
            };
            _db.SubscriptionBillingStates.Add(state);
        }
        else
        {
            // Refresh state on re-enrollment (e.g. after a manual bill revives a Stopped subscription)
            existing.RecurringBillingPlanId = plan.Id;
            existing.BillingAnchorDate      = subscription.StartDate;
            existing.NextBillingDate        = nextBillingDate;
            existing.NextAttemptDate        = nextBillingDate;
            existing.CurrentAttemptIndex    = 0;
            existing.Status                 = RecurringBillingStatus.Active;
            existing.IsDeleted              = false;
            existing.LastUpdateDate         = now;
            existing.LastUpdateBy           = actor;
        }

        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> ResolveProductIdAsync(MembershipSubscription subscription, CancellationToken ct)
    {
        // Subscription → MembershipLevel → Product (via Product.MembershipLevelId)
        if (subscription.MembershipLevel is null)
        {
            await _db.Entry(subscription).Reference(s => s.MembershipLevel).LoadAsync(ct);
        }

        if (subscription.MembershipLevel is null)
            return null;

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipLevelId == subscription.MembershipLevelId && !p.IsDeleted, ct);

        return product?.Id;
    }
}
