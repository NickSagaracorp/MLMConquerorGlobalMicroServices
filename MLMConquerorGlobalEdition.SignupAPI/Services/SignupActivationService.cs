using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <inheritdoc />
public class SignupActivationService : ISignupActivationService
{
    private readonly AppDbContext                       _db;
    private readonly ISponsorBonusService               _sponsorBonus;
    private readonly IFastStartBonusService             _fastStartBonus;
    private readonly IRecurringBillingEnrollmentService _recurringBillingEnrollment;
    private readonly ISignupOrderClaim                  _orderClaim;

    public SignupActivationService(
        AppDbContext db,
        ISponsorBonusService sponsorBonus,
        IFastStartBonusService fastStartBonus,
        IRecurringBillingEnrollmentService recurringBillingEnrollment,
        // Opcional para no tocar las construcciones existentes; el de verdad es SignupOrderClaim.
        ISignupOrderClaim? orderClaim = null)
    {
        _db                         = db;
        _sponsorBonus               = sponsorBonus;
        _fastStartBonus             = fastStartBonus;
        _recurringBillingEnrollment = recurringBillingEnrollment;
        _orderClaim                 = orderClaim ?? new SignupOrderClaim(db);
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

    public async Task<Result<MemberProfile>> ActivatePendingSignupAsync(Orders order, DateTime now, CancellationToken ct)
    {
        var member = await _db.MemberProfiles.FirstOrDefaultAsync(m => m.MemberId == order.MemberId, ct);
        if (member is null)
            return Result<MemberProfile>.Failure("MEMBER_NOT_FOUND", "Associated member not found.");

        // El pedido pasa a Completed de forma atómica ANTES de leer la suscripción y de tocar nada más:
        // si otro pago (cripto, otro QR de Entrax, el mismo pago por otro camino) lo activó entre tanto,
        // este no activa nada y recibe ORDER_ALREADY_SETTLED, no SUBSCRIPTION_NOT_FOUND (el ganador ya
        // la activó). Si algo de lo que sigue falla, quien llama deshace su transacción y el reclamo.
        if (!await _orderClaim.TryCompleteAsync(order, now, member.Email, ct))
            return Result<MemberProfile>.Failure(SignupOrderClaim.AlreadySettledCode,
                "This signup was already activated by another payment.");

        var subscription = await _db.MembershipSubscriptions.FirstOrDefaultAsync(
            s => s.MemberId == member.MemberId && s.SubscriptionStatus == MembershipStatus.Pending, ct);
        if (subscription is null)
            return Result<MemberProfile>.Failure("SUBSCRIPTION_NOT_FOUND", "Pending subscription not found.");

        var totalQualPoints = await _db.OrderDetails
            .AsNoTracking()
            .Where(od => od.OrderId == order.Id)
            .Join(_db.Products.AsNoTracking(), od => od.ProductId, p => p.Id, (od, p) => p.QualificationPoins)
            .SumAsync(ct);

        await ActivateAsync(order, member, subscription, totalQualPoints, now, member.Email, ct);
        return Result<MemberProfile>.Success(member);
    }

    private static List<string> ParseHierarchyPath(string hierarchyPath)
        => hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
}
