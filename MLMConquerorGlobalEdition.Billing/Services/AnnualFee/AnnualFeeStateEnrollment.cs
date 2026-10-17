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
        // Los que fallaron fila a fila en esta ejecución: no se vuelven a pedir (si no, el lote siguiente
        // los traería otra vez y la ejecución no avanzaría).
        var failed  = new List<string>();

        for (var batch = 0; batch < maxBatches; batch++)
        {
            var candidates = await MissingAmbassadors()
                .Where(m => !failed.Contains(m.MemberId))
                .OrderBy(m => m.MemberId)
                .Select(m => new { m.MemberId, m.EnrollDate })
                .Take(batchSize)
                .ToListAsync(ct);
            if (candidates.Count == 0) break;

            var now    = _clock.Now;
            var states = candidates.Select(c => NewState(c.MemberId, c.EnrollDate, planId, now)).ToList();
            _db.SubscriptionBillingStates.AddRange(states);

            try
            {
                await _db.SaveChangesAsync(ct);
                created += states.Count;
                Detach(states);
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                // Revisión M1: el lote entero no se pierde por una fila. Se suelta lo añadido (solo lo
                // nuestro: no se limpia el contexto) y se reintenta de uno en uno; la que vuelva a fallar
                // (otra instancia la creó, o un dato que la base rechaza) se registra y se salta.
                Detach(states);
                _logger.LogWarning(ex, "Cuota anual: un lote de {Count} altas falló al guardar; se reintenta fila a fila.", states.Count);

                foreach (var c in candidates)
                {
                    var one = NewState(c.MemberId, c.EnrollDate, planId, now);
                    _db.SubscriptionBillingStates.Add(one);
                    try
                    {
                        await _db.SaveChangesAsync(ct);
                        created++;
                    }
                    catch (DbUpdateException rowEx) when (rowEx is not DbUpdateConcurrencyException)
                    {
                        failed.Add(c.MemberId);
                        _logger.LogError(rowEx, "Cuota anual: no se pudo crear el estado de {MemberId}.", c.MemberId);
                    }
                    finally
                    {
                        Detach(new[] { one });
                    }
                }
            }

            if (candidates.Count < batchSize) break;
        }

        if (failed.Count > 0)
            _logger.LogError("Cuota anual: {Count} estados no se pudieron crear en esta ejecución: {MemberIds}.",
                failed.Count, string.Join(", ", failed.Take(50)));
        return created;
    }

    public async Task<SubscriptionBillingState?> EnsureForMemberAsync(string memberId, CancellationToken ct = default)
    {
        // Con los borrados: el índice único los incluye, así que uno borrado también "existe".
        var existing = await AnyStateWithPlan().FirstOrDefaultAsync(s => s.MemberId == memberId, ct);
        if (existing is not null) return Usable(existing, memberId);

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
            // Revisión I4: solo se suelta lo que añadimos (el contexto puede ser compartido). Si otra
            // petición lo creó a la vez, la fila está y se usa la suya; si no hay fila, el fallo era otro
            // y se relanza: no se traga.
            Detach(new[] { state });
            var theirs = await AnyStateWithPlan().FirstOrDefaultAsync(s => s.MemberId == memberId, ct);
            if (theirs is null) throw;
            _logger.LogInformation(ex, "Cuota anual: el estado de {MemberId} lo creó otra petición a la vez.", memberId);
            return Usable(theirs, memberId);
        }

        return await AnyStateWithPlan().FirstOrDefaultAsync(s => s.MemberId == memberId && !s.IsDeleted, ct);
    }

    /// <summary>
    /// Un estado borrado no se restaura ni se sustituye: alguien lo borró a propósito y restaurarlo en
    /// silencio volvería a cobrar. Se informa y el miembro queda sin estado hasta que alguien lo revise.
    /// </summary>
    private SubscriptionBillingState? Usable(SubscriptionBillingState state, string memberId)
    {
        if (!state.IsDeleted) return state;
        _logger.LogWarning(
            "Cuota anual: el estado {StateId} de {MemberId} está borrado; no se restaura ni se crea otro.",
            state.Id, memberId);
        return null;
    }

    private void Detach(IEnumerable<SubscriptionBillingState> states)
    {
        foreach (var s in states) _db.Entry(s).State = EntityState.Detached;
    }

    private IQueryable<SubscriptionBillingState> AnyStateWithPlan() =>
        _db.SubscriptionBillingStates
           .IgnoreQueryFilters()
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
