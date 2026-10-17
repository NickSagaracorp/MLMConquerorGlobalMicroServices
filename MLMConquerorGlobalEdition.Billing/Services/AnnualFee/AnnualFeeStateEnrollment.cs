using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

public interface IAnnualFeeStateEnrollment
{
    /// <summary>
    /// Crea los estados de cuota que faltan, por lotes de <paramref name="batchSize"/> y como mucho
    /// <paramref name="maxBatches"/> lotes. Devuelve cuántos creó. Idempotente.
    /// </summary>
    Task<int> EnsureMissingAsync(int batchSize, int maxBatches, CancellationToken ct = default);

    /// <summary>El estado de cuota del miembro con su plan y productos; lo crea si falta. Null si no le toca.</summary>
    Task<SubscriptionBillingState?> EnsureForMemberAsync(string memberId, CancellationToken ct = default);
}

/// <summary>
/// Da de alta los estados de cuota anual (Plan K, K3): uno por Ambassador no borrado y no Terminated,
/// en el plan marcado IsAnnualFeePlan, con el primer aniversario de la inscripción que no haya pasado.
/// También a quien tiene la membresía en HoldByBilling: no se le cobra mientras siga así (K5, decisión
/// de Nick), pero el estado queda listo para cuando salga de la espera.
/// No migra nada del legado. UX_SubscriptionBillingStates_AnnualFeeMember impide duplicar aunque dos
/// instancias corran a la vez.
/// </summary>
public sealed class AnnualFeeStateEnrollment : IAnnualFeeStateEnrollment
{
    private const string Actor = "annual-fee-enrollment";

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AnnualFeeStateEnrollment> _logger;

    public AnnualFeeStateEnrollment(AppDbContext db, IDateTimeProvider clock, ILogger<AnnualFeeStateEnrollment> logger)
    {
        _db     = db;
        _clock  = clock;
        _logger = logger;
    }

    public async Task<int> EnsureMissingAsync(int batchSize, int maxBatches, CancellationToken ct = default)
    {
        var plan = await AnnualFeePlanQueries.ActivePlanAsync(_db, ct);
        if (plan is null)
        {
            _logger.LogWarning("Cuota anual: no hay plan activo marcado como IsAnnualFeePlan; no se crean estados.");
            return 0;
        }

        var planId  = plan.Id;
        var created = 0;

        for (var batch = 0; batch < maxBatches; batch++)
        {
            var candidates = await MissingAmbassadors()
                .OrderBy(m => m.MemberId)
                .Select(m => new { m.MemberId, m.EnrollDate })
                .Take(batchSize)
                .ToListAsync(ct);
            if (candidates.Count == 0) break;

            var now = _clock.Now;
            foreach (var c in candidates)
                _db.SubscriptionBillingStates.Add(NewState(c.MemberId, c.EnrollDate, planId, now));

            try
            {
                await _db.SaveChangesAsync(ct);
                created += candidates.Count;
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                // Otra instancia creó alguno a la vez (índice único). Se para aquí; la siguiente ejecución
                // sigue donde quede. No se reintenta en bucle: si el fallo fuera otro, se repetiría igual.
                _db.ChangeTracker.Clear();
                _logger.LogWarning(ex, "Cuota anual: un lote de alta chocó al guardar; se seguirá en la siguiente ejecución.");
                break;
            }

            _db.ChangeTracker.Clear();
            if (candidates.Count < batchSize) break;
        }

        return created;
    }

    public async Task<SubscriptionBillingState?> EnsureForMemberAsync(string memberId, CancellationToken ct = default)
    {
        var existing = await StateWithPlan().FirstOrDefaultAsync(s => s.MemberId == memberId, ct);
        if (existing is not null) return existing;

        var plan = await AnnualFeePlanQueries.ActivePlanAsync(_db, ct);
        if (plan is null) return null;

        var member = await Eligible()
            .Where(m => m.MemberId == memberId)
            .Select(m => new { m.MemberId, m.EnrollDate })
            .FirstOrDefaultAsync(ct);
        if (member is null) return null;

        var state = NewState(member.MemberId, member.EnrollDate, plan.Id, _clock.Now);
        _db.SubscriptionBillingStates.Add(state);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            // Lo creó otra petición a la vez: se usa el suyo.
            _db.ChangeTracker.Clear();
            _logger.LogInformation(ex, "Cuota anual: el estado de {MemberId} lo creó otra petición a la vez.", memberId);
        }

        return await StateWithPlan().FirstOrDefaultAsync(s => s.MemberId == memberId, ct);
    }

    private IQueryable<SubscriptionBillingState> StateWithPlan() =>
        _db.SubscriptionBillingStates
           .Include(s => s.Plan).ThenInclude(p => p!.PlanProducts)
           .Where(s => s.Purpose == BillingStatePurpose.AnnualFee);

    private IQueryable<MemberProfile> Eligible() =>
        _db.MemberProfiles.AsNoTracking()
           .Where(m => m.MemberType == MemberType.Ambassador
                    && !m.IsDeleted
                    && m.Status != MemberAccountStatus.Terminated);

    /// <summary>
    /// Ambassadors sin estado de cuota. IgnoreQueryFilters en los estados: el índice único incluye los
    /// borrados lógicos, así que uno borrado también cuenta como "ya tiene".
    /// </summary>
    private IQueryable<MemberProfile> MissingAmbassadors() =>
        Eligible().Where(m => !_db.SubscriptionBillingStates.IgnoreQueryFilters()
                                  .Any(s => s.Purpose == BillingStatePurpose.AnnualFee && s.MemberId == m.MemberId));

    private static SubscriptionBillingState NewState(string memberId, DateTime enrollDate, int planId, DateTime now)
    {
        var due = AnnualFeeSchedule.FirstDueOnCreation(enrollDate, now);
        return new SubscriptionBillingState
        {
            Purpose                   = BillingStatePurpose.AnnualFee,
            MembershipSubscriptionId  = null,
            MemberId                  = memberId,
            RecurringBillingPlanId    = planId,
            BillingAnchorDate         = enrollDate.Date,
            LastSuccessfulBillingDate = null,
            NextBillingDate           = due,
            NextAttemptDate           = due,
            CurrentAttemptIndex       = 0,
            Status                    = RecurringBillingStatus.Active,
            CreatedBy                 = Actor,
            CreationDate              = now,
            LastUpdateDate            = now
        };
    }
}
