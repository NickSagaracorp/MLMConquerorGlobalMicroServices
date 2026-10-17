using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>La tarjeta de la cuota anual: lo que se enseña y la preferencia (Plan K, K21).</summary>
public interface IAnnualFeeOverviewService
{
    Task<AnnualFeeOverviewDto> GetAsync(string memberId, CancellationToken ct = default);

    /// <summary>Guarda la preferencia de pagar las cuotas con el saldo. False si el miembro no es Ambassador.</summary>
    Task<bool> SetFeesFromBalanceAsync(string memberId, bool value, string actor, CancellationToken ct = default);
}

/// <summary>
/// Solo lee (salvo la preferencia). Lo usan el BizCenter (el miembro del token) y el Admin (el de la ruta,
/// solo lectura). Decide "se puede pagar" con las mismas reglas que Billing (AnnualFeeEligibility), para
/// que la pantalla no ofrezca lo que Billing va a rechazar.
/// </summary>
public sealed class AnnualFeeOverviewService : IAnnualFeeOverviewService
{
    public const int RecentPaymentsShown = 5;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public AnnualFeeOverviewService(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<AnnualFeeOverviewDto> GetAsync(string memberId, CancellationToken ct = default)
    {
        var now    = _clock.Now;
        var member = await _db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
        // Con la membresía vigente en HoldByBilling (R5) o cancelada (M-8): las mismas reglas que Billing.
        var facts  = await AnnualFeeMemberQueries.FactsAsync(_db, memberId, now, ct);
        if (!facts.Exists || facts.MemberType != MemberType.Ambassador)
            return new AnnualFeeOverviewDto();

        var plan  = await AnnualFeePlanQueries.ActivePlanAsync(_db, ct);
        var price = plan is null ? null : await AnnualFeePlanQueries.PriceAsync(_db, plan, ct);
        var state = await _db.SubscriptionBillingStates.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Purpose == BillingStatePurpose.AnnualFee && s.MemberId == memberId, ct);

        // Sin estado todavía (el trabajo de alta aún no ha pasado): el vencimiento que tendrá al crearse.
        var nextDue = state?.NextBillingDate.Date ?? AnnualFeeSchedule.FirstDueOnCreation(facts.EnrollDate, now);

        var underReview = state is not null
            && await AnnualFeePaymentQueries.LiveForCycle(_db, state.Id, nextDue).AsNoTracking()
                   .AnyAsync(p => p.Status == AnnualFeePaymentStatus.Pending
                               || p.Status == AnnualFeePaymentStatus.NeedsReconciliation, ct);

        var reason = plan is null || price is null ? AnnualFeeErrors.PlanNotConfigured
                   : underReview                   ? AnnualFeeErrors.NeedsReview
                   : AnnualFeeEligibility.ForPayNow(facts, nextDue, now);

        // Los enumerados se proyectan tal cual y se pasan a texto en memoria (no depende de que EF traduzca ToString).
        var payments = (await _db.AnnualFeePayments.AsNoTracking()
                .Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.CreationDate)
                .Take(RecentPaymentsShown)
                .Select(p => new { p.Id, Date = p.CompletedAt ?? p.CreationDate, p.CycleDueDate, p.Method, p.Status, p.Amount })
                .ToListAsync(ct))
            .Select(p => new AnnualFeePaymentRowDto
            {
                PaymentId = p.Id, Date = p.Date, CycleDueDate = p.CycleDueDate,
                Method = p.Method.ToString(), Status = p.Status.ToString(), Amount = p.Amount
            })
            .ToList();

        return new AnnualFeeOverviewDto
        {
            Applies            = true,
            Status             = StatusOf(facts.KeepQualified, underReview, nextDue, now),
            Amount             = price?.Amount,
            NextDueDate        = nextDue,
            LastPaidDate       = state?.LastSuccessfulBillingDate,
            CanPayNow          = reason is null,
            NotPayableReason   = reason,
            AvailableFeeTokens = await AnnualFeeTokenQueries.Available(_db, memberId, now).CountAsync(ct),
            FeesFromBalance    = member!.FeesFromBalance,
            RecentPayments     = payments
        };
    }

    public async Task<bool> SetFeesFromBalanceAsync(string memberId, bool value, string actor, CancellationToken ct = default)
    {
        var member = await _db.MemberProfiles
            // Revisión final (M-e): explícito aunque haya filtro global, por si alguien lee sin filtros.
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.MemberType == MemberType.Ambassador && !m.IsDeleted, ct);
        if (member is null) return false;

        member.FeesFromBalance = value;
        member.LastUpdateDate  = _clock.Now;
        member.LastUpdateBy    = actor;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string StatusOf(bool keepQualified, bool underReview, DateTime nextDue, DateTime now)
    {
        if (keepQualified)                                          return AnnualFeeStatuses.Waived;
        if (underReview)                                            return AnnualFeeStatuses.UnderReview;
        if (nextDue > now.Date.AddDays(AnnualFeeRules.PayAheadDays)) return AnnualFeeStatuses.UpToDate;
        if (nextDue > now.Date)                                     return AnnualFeeStatuses.DueSoon;
        return AnnualFeeStatuses.Due;
    }
}
