using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
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
        // Plan Q (DQ10): el nivel de alta guardado (regla N2), por si la eleccion de productos no lo dejo puesto.
        await SignupMembershipLevel.RefreshAsync(_db, member, ct);

        // El mes de membresía arranca cuando el dinero está cobrado, no cuando se rellenó el
        // formulario. En la vía de cripto eso puede ser días después.
        // Plan Q (DQ16): los puntos que aporta la suscripcion activa salen de su ultimo pedido, que es el del alta.
        subscription.LastOrderId      ??= order.Id;
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
                .CurrentOfAsync(member.SponsorMemberId, ct); // nodo vigente (Plan M, M2)

            if (sponsorNode is not null)
            {
                // Plan Q (Bloque 4): UNA fila por alta. ApplyMemberStatisticDeltasJob la expande en SQL por los
                // ascendientes del Node del patrocinador (el mismo delta que antes, en cada uno; los patrocinados solo
                // en el). Antes, una fila por ascendiente en esta transaccion: 29,4 millones de filas con 1.000.000 de
                // altas (Plan P, Bloque D). Los puntos (DQ16) los encola MembershipPointsDeltas al pasar la suscripcion a
                // Active (el propio incluido); aqui solo el tamano del equipo y los patrocinados.
                await _db.MemberStatisticDeltas.AddAsync(new MemberStatisticDelta
                {
                    MemberId                       = member.SponsorMemberId,
                    Scope                          = MemberStatisticDeltaScope.SponsorLine,
                    EnrollmentPointsDelta          = 0,
                    EnrollmentTeamSizeDelta        = 1,
                    QualifiedSponsoredMembersDelta = 1,
                    SourceMemberId                 = member.MemberId,
                    IsApplied                      = false,
                    CreatedBy                      = actorEmail,
                    CreationDate                   = now
                }, ct);
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
}
