using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Billing;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;

/// <summary>
/// Cambio de estado de un miembro desde administración (PUT api/v1/admin/members/{memberId}/status).
///
/// Ponerlo en Active es una ACTIVACIÓN MANUAL Y DELIBERADA del personal: pasa la suscripción Pending a Active
/// y el pedido Pending a Completed SIN COBRO (p. ej. un pago verificado por fuera). Queda en
/// MemberStatusHistory con quién lo hizo y el motivo. Lo que no puede es pisar un pago en vuelo: si el pedido
/// del alta (el primero del miembro) está en Processing —cripto o Entrax esperando— o hay un pago de Entrax en
/// AwaitingPayment, responde PAYMENT_IN_FLIGHT (409) sin cambiar nada; ahí lo correcto es confirmar la
/// cripto o hacer Re-check.
/// <para>
/// Plan Q (Q3, decidido por Nick): Suspended o Inactive retienen (OnHold) las membresias activas y paran su cobro;
/// Terminated las cancela; volver a Active reactiva solo las que retuvo el admin (<see cref="AccountStatusMemberships"/>).
/// Los puntos de toda la linea los resta y los devuelve el interceptor. Volver a Active desde Terminated no se bloquea
/// (como hasta ahora), pero no reactiva nada: sus membresias quedaron canceladas.
/// </para>
/// </summary>
public class UpdateMemberStatusHandler : IRequestHandler<UpdateMemberStatusCommand, Result<AdminMemberDto>>
{
    /// <summary>Hay un cobro del alta en curso: no se activa a mano. 409.</summary>
    public const string PaymentInFlightCode = "PAYMENT_IN_FLIGHT";

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICacheService _cache;

    public UpdateMemberStatusHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        ICacheService cache)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _cache = cache;
    }

    public async Task<Result<AdminMemberDto>> Handle(
        UpdateMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == request.MemberId, cancellationToken);

        if (member is null)
            return Result<AdminMemberDto>.Failure("MEMBER_NOT_FOUND", $"Member '{request.MemberId}' not found.");

        if (request.Request.Status == MemberAccountStatus.Active
            && await HasPaymentInFlightAsync(member.MemberId, cancellationToken))
            return Result<AdminMemberDto>.Failure(PaymentInFlightCode,
                "This member has a signup payment in progress (crypto or SBP). Confirm the crypto payment or use " +
                "Re-check instead of activating manually.");

        // Plan Q (Q3): el candado de puntos del miembro ANTES de leer sus suscripciones (como la subida y la bajada): sin el,
        // un cambio de nivel a la vez podia dejar una activa nueva sin retener. El guardado lo reutiliza y lo suelta al
        // confirmar; si no se llega a guardar, el finally.
        await MemberPointsLocks.AcquireAsync(_db, new[] { member.MemberId }, cancellationToken);
        try
        {
            return await ChangeAsync(member, request, cancellationToken);
        }
        finally
        {
            await MemberPointsLocks.ReleaseAsync(_db);
        }
    }

    private async Task<Result<AdminMemberDto>> ChangeAsync(
        MemberProfile member, UpdateMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var oldStatus = member.Status;
        var now = _dateTime.Now;

        member.Status = request.Request.Status;
        member.LastUpdateDate = now;
        member.LastUpdateBy = _currentUser.UserId;

        var history = new MemberStatusHistory
        {
            MemberId = member.MemberId,
            OldStatus = oldStatus,
            NewStatus = request.Request.Status,
            Reason = request.Request.Reason,
            ChangedAt = now,
            CreationDate = now,
            CreatedBy = _currentUser.UserId
        };

        await _db.MemberStatusHistories.AddAsync(history, cancellationToken);

        // When an admin activates a member, sync the pending membership subscription.
        if (request.Request.Status == MemberAccountStatus.Active)
        {
            var subscription = await _db.MembershipSubscriptions
                .Where(s => s.MemberId == member.MemberId && s.SubscriptionStatus == MembershipStatus.Pending)
                .OrderByDescending(s => s.CreationDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is not null)
            {
                var startDate = subscription.StartDate == default ? now : subscription.StartDate;
                subscription.SubscriptionStatus = MembershipStatus.Active;
                subscription.StartDate          = startDate;
                subscription.EndDate            = startDate.AddMonths(1);
                subscription.RenewalDate        = startDate.AddMonths(1);
                subscription.LastUpdateDate     = now;
                subscription.LastUpdateBy       = _currentUser.UserId;
            }

            // Complete the most recent pending order so qualification points are counted.
            var pendingOrder = await _db.Orders
                .Where(o => o.MemberId == member.MemberId && o.Status == OrderStatus.Pending)
                .OrderByDescending(o => o.CreationDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (pendingOrder is not null)
            {
                pendingOrder.Status          = OrderStatus.Completed;
                pendingOrder.OrderDate       = pendingOrder.OrderDate == default ? now : pendingOrder.OrderDate;
                pendingOrder.LastUpdateDate  = now;
                pendingOrder.LastUpdateBy    = _currentUser.UserId;
            }

            // Plan Q (Q3): vuelven a Active solo las que retuvo el admin (HeldByAccountStatus); su cobro se reanuda. Una
            // OnHold de otro origen se queda como esta. Tras Terminated no hay nada que reanudar: se cancelaron.
            await AccountStatusMemberships.RestoreAsync(_db, member.MemberId, now, _currentUser.UserId, cancellationToken);
        }
        // Plan Q (Q3, decidido por Nick): suspender o inactivar retiene las activas (OnHold, con la marca) y para su cobro;
        // terminar cancela las activas y las retenidas por el admin. El interceptor resta los puntos de toda la linea.
        else if (request.Request.Status is MemberAccountStatus.Suspended or MemberAccountStatus.Inactive)
        {
            await AccountStatusMemberships.HoldAsync(
                _db, member.MemberId, request.Request.Status, now, _currentUser.UserId, cancellationToken);
        }
        else if (request.Request.Status == MemberAccountStatus.Terminated)
        {
            await AccountStatusMemberships.CancelForTerminationAsync(
                _db, member.MemberId, now, _currentUser.UserId, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Cache invalidation — the member's profile snapshot in BizCenter and
        // any admin/CEO dashboards that aggregated by status are stale now.
        // We can't pattern-delete the admin:members:p{x}:s{y}:f{z} list keys
        // without Redis SCAN, so those rely on their 2-minute TTL. Cleared:
        await _cache.RemoveAsync(CacheKeys.MemberProfile(member.MemberId), cancellationToken);
        await _cache.RemoveAsync(CacheKeys.AdminCeoDashboard, cancellationToken);
        await _cache.RemoveAsync(CacheKeys.AdminFinancialDashboard, cancellationToken);
        await _cache.RemoveAsync(CacheKeys.AdminGrowthDashboard, cancellationToken);
        await _cache.RemoveAsync(CacheKeys.AdminHealthDashboard, cancellationToken);

        var dto = new AdminMemberDto
        {
            MemberId = member.MemberId,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Phone = member.Phone,
            Country = member.Country,
            Status = member.Status.ToString(),
            MemberType = member.MemberType.ToString(),
            EnrollDate = member.EnrollDate,
            SponsorMemberId = member.SponsorMemberId,
            CreationDate = member.CreationDate
        };

        return Result<AdminMemberDto>.Success(dto);
    }

    /// <summary>
    /// El pedido del alta (el primero del miembro, por fecha) está en Processing, o hay un pago de Entrax del
    /// miembro esperando cobro.
    /// </summary>
    private async Task<bool> HasPaymentInFlightAsync(string memberId, CancellationToken ct)
    {
        var signupOrderStatus = await _db.Orders.AsNoTracking()
            .Where(o => o.MemberId == memberId)
            .OrderBy(o => o.OrderDate).ThenBy(o => o.CreationDate)
            .Select(o => (OrderStatus?)o.Status)
            .FirstOrDefaultAsync(ct);

        if (signupOrderStatus == OrderStatus.Processing)
            return true;

        return await _db.EntraxPayments.AsNoTracking()
            .AnyAsync(p => p.MemberId == memberId && p.Status == EntraxPaymentStatus.AwaitingPayment, ct);
    }
}
