using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <inheritdoc />
public class SignupActivationService : ISignupActivationService
{
    private readonly AppDbContext                       _db;
    private readonly ISponsorBonusService               _sponsorBonus;
    private readonly IFastStartBonusService             _fastStartBonus;
    private readonly IRecurringBillingEnrollmentService _recurringBillingEnrollment;

    public SignupActivationService(
        AppDbContext db,
        ISponsorBonusService sponsorBonus,
        IFastStartBonusService fastStartBonus,
        IRecurringBillingEnrollmentService recurringBillingEnrollment)
    {
        _db                         = db;
        _sponsorBonus               = sponsorBonus;
        _fastStartBonus             = fastStartBonus;
        _recurringBillingEnrollment = recurringBillingEnrollment;
    }

    public async Task ActivateAsync(
        Orders order,
        MemberProfile member,
        MembershipSubscription subscription,
        int totalQualificationPoints,
        DateTime now,
        string actorEmail,
        CancellationToken ct)
    {
        order.Status         = OrderStatus.Completed;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = actorEmail;

        member.Status         = MemberAccountStatus.Active;
        member.LastUpdateDate = now;
        member.LastUpdateBy   = actorEmail;

        // El mes de membresía arranca cuando el dinero está cobrado, no cuando se rellenó el
        // formulario. En la vía de cripto eso puede ser días después.
        subscription.SubscriptionStatus = MembershipStatus.Active;
        subscription.StartDate          = now;
        subscription.EndDate            = now.AddMonths(1);
        subscription.RenewalDate        = now.AddMonths(1);
        subscription.LastUpdateDate     = now;
        subscription.LastUpdateBy       = actorEmail;

        if (!string.IsNullOrEmpty(member.SponsorMemberId))
        {
            var sponsorNode = await _db.GenealogyTree
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.MemberId == member.SponsorMemberId, ct);

            if (sponsorNode is not null)
            {
                var ancestorIds = ParseHierarchyPath(sponsorNode.HierarchyPath);

                // Sprint-16 — tubería de consistencia eventual. Antes este bucle ejecutaba un
                // MERGE … WITH (HOLDLOCK) por ancestro (arreglo del Bug A del Sprint-15), que era
                // correcto pero se serializaba en las filas compartidas cerca de la raíz y subía
                // la latencia media del alta a ~16s con 350 concurrentes.
                //
                // Ahora se encola una fila de MemberStatisticDelta por ancestro en un solo
                // insert por lotes. ApplyMemberStatisticDeltasJob (recurrente, cada minuto, cola
                // "signups") agrupa por MemberId y consolida el delta sumado en MemberStatistics
                // con un MERGE por upline distinto y ciclo — así 350 altas concurrentes bajo el
                // mismo upline producen un MERGE en vez de 350.
                //
                // Retraso de frescura: hasta ~1 min. La evaluación de rango corre cada 5 min vía
                // ProcessRankQueueJob, así que los deltas siempre están al día antes de evaluar;
                // por si acaso, el job de aplicación reencola una entrada en RankEvaluationQueue
                // por cada miembro cuyas estadísticas acaba de tocar (espeja SignupAmbassadorHandler L255).
                var deltas = new List<MemberStatisticDelta>(ancestorIds.Count);
                foreach (var ancestorId in ancestorIds)
                {
                    var qualDelta = ancestorId == member.SponsorMemberId ? 1 : 0;
                    deltas.Add(new MemberStatisticDelta
                    {
                        MemberId                       = ancestorId,
                        EnrollmentPointsDelta          = totalQualificationPoints,
                        EnrollmentTeamSizeDelta        = 1,
                        QualifiedSponsoredMembersDelta = qualDelta,
                        SourceMemberId                 = member.MemberId,
                        IsApplied                      = false,
                        CreatedBy                      = actorEmail,
                        CreationDate                   = now
                    });
                }

                if (deltas.Count > 0)
                    await _db.MemberStatisticDeltas.AddRangeAsync(deltas, ct);
            }
        }

        // Los dos servicios son idempotentes por SourceOrderId: si esto se ejecutara dos veces
        // para el mismo pedido no habría comisión duplicada. Es la red debajo de la red.
        await _sponsorBonus.ComputeAsync(
            member.SponsorMemberId, member.MemberId, order.Id,
            order.TotalAmount, actorEmail, now, ct);

        await _fastStartBonus.ComputeAsync(
            member.SponsorMemberId, member.MemberId, order.Id,
            now, actorEmail, ct);

        // Crea o actualiza el SubscriptionBillingState para que el barrido de morosidad sepa que
        // esta suscripción entra en cobro recurrente desde hoy.
        await _recurringBillingEnrollment.EnsureStateForSubscriptionAsync(subscription, actorEmail, ct);
    }

    private static List<string> ParseHierarchyPath(string hierarchyPath)
        => hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
}
