using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

public enum EntraxSettlementSource { Webhook, Poll, Sweep, Admin }

public enum EntraxSettlementOutcome
{
    NotFound,
    Activated,
    AlreadyResolved,
    StillPending,
    Failed,
    Expired,
    ToReview,
    ProviderUnavailable
}

public interface IEntraxSignupSettlement
{
    /// <summary>Lleva el pago a su estado real según Entrax y, si está pagado y cuadra, activa el alta.</summary>
    Task<EntraxSettlementOutcome> SettleAsync(string entraxPaymentId, EntraxSettlementSource source, CancellationToken ct);
}

/// <summary>
/// EL ÚNICO CAMINO QUE ACTIVA UN ALTA PAGADA CON ENTRAX. Llegan aquí el webhook, la pantalla que
/// consulta el estado, el barrido de Hangfire y el Re-check del admin; ninguno activa por su cuenta.
///
/// Lo que el legado hacía mal y aquí no:
///  - no se fía de quien avisa: SIEMPRE re-consulta GET /payment/{id};
///  - exige paid, USD e importe ≥ lo pedido (y ≥ el total actual del pedido); si no cuadra, ToReview
///    con un Critical en el log y sin activar nada;
///  - un pago activa UNA alta: se reclama con un UPDATE condicional (no Paid ni ToReview) como primera
///    sentencia de la transacción, y el que llega segundo (webhook y pantalla a la vez) lo encuentra ya
///    liquidado y devuelve AlreadyResolved, nunca ToReview; si pierde después en el pedido, relee el
///    pago y, si está Paid, también AlreadyResolved. Una DbUpdateConcurrencyException se relee (máx. 3);
///  - un pedido lo activa UN pago: la transición atómica del pedido (SignupOrderClaim) y el índice
///    UX_EntraxPayments_OrderId_Consumed hacen que el segundo (otro QR, la cripto) quede ToReview con
///    ORDER_ALREADY_SETTLED;
///  - el webhook no depende del navegador: el alta se activa aunque nadie tenga la página abierta.
///
/// Activar es exactamente lo de la confirmación cripto (ISignupActivationService.ActivatePendingSignupAsync)
/// más el pago registrado en PaymentHistory con gateway "Entrax" y el paymentId externo.
///
/// Una vez empezado no se cancela con la petición: lo que mueve dinero usa CancellationToken.None.
/// </summary>
public sealed class EntraxSignupSettlement : IEntraxSignupSettlement
{
    /// <summary>Lo que se espera a que una fila sin paymentId reciba uno (por el callback) antes de darla por fallida.</summary>
    public static readonly TimeSpan UnissuedGrace = TimeSpan.FromMinutes(30);

    private const int    MaxAttempts = 3;
    private const string Actor       = EntraxGateway.Name;

    /// <summary>Índice único filtrado que impide que dos pagos de Entrax consuman el mismo pedido.</summary>
    private const string OrderConsumedIndex = "UX_EntraxPayments_OrderId_Consumed";

    private readonly AppDbContext                     _db;
    private readonly IEntraxClient                    _client;
    private readonly ISignupActivationService         _activation;
    private readonly UserManager<ApplicationUser>     _users;
    private readonly IDateTimeProvider                _clock;
    private readonly ILogger<EntraxSignupSettlement>  _logger;

    public EntraxSignupSettlement(
        AppDbContext db,
        IEntraxClient client,
        ISignupActivationService activation,
        UserManager<ApplicationUser> users,
        IDateTimeProvider clock,
        ILogger<EntraxSignupSettlement> logger)
    {
        _db         = db;
        _client     = client;
        _activation = activation;
        _users      = users;
        _clock      = clock;
        _logger     = logger;
    }

    public async Task<EntraxSettlementOutcome> SettleAsync(
        string entraxPaymentId, EntraxSettlementSource source, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SettleOnceAsync(entraxPaymentId, source);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // Otro camino (webhook, pantalla, barrido, admin, o la cripto del mismo pedido) guardó
                // la misma fila o el mismo miembro entre la lectura y el guardado. Se relee y se decide
                // con lo que haya ahora: ya liquidado, o el pedido ya activado (→ ToReview).
                _db.ChangeTracker.Clear();
                _logger.LogWarning("Entrax: carrera al liquidar {EntraxPaymentId} ({Source}); se vuelve a leer.",
                    entraxPaymentId, source);
            }
        }
    }

    private async Task<EntraxSettlementOutcome> SettleOnceAsync(string id, EntraxSettlementSource source)
    {
        var none = CancellationToken.None;
        var now  = _clock.Now;

        var payment = await _db.EntraxPayments.FirstOrDefaultAsync(p => p.Id == id, none);
        if (payment is null)
            return EntraxSettlementOutcome.NotFound;

        if (payment.Status == EntraxPaymentStatus.Paid)
        {
            // Ya activó. Si el proceso cayó entre guardar y activar el usuario, esto lo repara.
            await EnsureUserActiveAsync(payment.MemberId);
            return EntraxSettlementOutcome.AlreadyResolved;
        }

        if (payment.Status == EntraxPaymentStatus.ToReview)
            return EntraxSettlementOutcome.AlreadyResolved;

        if (payment.ExternalPaymentId is null)
        {
            if (now - payment.CreationDate < UnissuedGrace)
                return EntraxSettlementOutcome.StillPending;

            return await CloseAsync(payment, EntraxPaymentStatus.Failed, "NO_PAYMENT_ID", now);
        }

        var call = await _client.GetPaymentAsync(payment.ExternalPaymentId, none);
        _db.EntraxApiCalls.Add(EntraxCallLog.From(EntraxCallLog.GetPayment, call.HttpStatus, call.ErrorCode, call.ElapsedMs,
            payment.ExternalOrderId, payment.ExternalPaymentId, call.Value?.Status, now, $"{Actor}:{source}"));
        payment.LastCheckedAt = now;

        // La consulta queda anotada pase lo que pase después (deshacer la activación no la borra).
        await _db.SaveChangesAsync(none);

        if (!call.Ok)
        {
            _logger.LogWarning("Entrax: no se pudo consultar el pago {ExternalPaymentId} ({Code}); se reintentará.",
                payment.ExternalPaymentId, call.ErrorCode);
            return EntraxSettlementOutcome.ProviderUnavailable;
        }

        var remote = call.Value!;
        if (!string.Equals(remote.PaymentId, payment.ExternalPaymentId, StringComparison.OrdinalIgnoreCase)
            || (remote.OrderId is not null && remote.OrderId != payment.ExternalOrderId))
            return await ReviewAsync(payment, "PROVIDER_MISMATCH", now, null);

        switch (remote.Status.Trim().ToLowerInvariant())
        {
            case "paid":
                return await SettlePaidAsync(payment, remote, now, source);
            case "failed":
                return await CloseAsync(payment, EntraxPaymentStatus.Failed, null, now);
            case "expired":
                return await CloseAsync(payment, EntraxPaymentStatus.Expired, null, now);
            default:
                return EntraxSettlementOutcome.StillPending;
        }
    }

    private async Task<EntraxSettlementOutcome> SettlePaidAsync(
        EntraxPayment payment, EntraxPaymentState remote, DateTime now, EntraxSettlementSource source)
    {
        var none = CancellationToken.None;

        var paidAt = payment.PaidAt ?? remote.CompletedAt?.LocalDateTime ?? now;
        payment.PaidAt        = paidAt;
        payment.PaidAmountUsd = remote.Original;

        if (!string.Equals(remote.Currency, EntraxGateway.Currency, StringComparison.OrdinalIgnoreCase))
            return await ReviewAsync(payment, "CURRENCY_MISMATCH", now, remote.Original);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, none);
        if (order is null)
            return await ReviewAsync(payment, "ORDER_NOT_FOUND", now, remote.Original);

        var expected = Math.Max(payment.AmountUsd, Math.Round(order.TotalAmount, 2, MidpointRounding.AwayFromZero));
        if (remote.Original < expected)
            return await ReviewAsync(payment, "AMOUNT_TOO_LOW", now, remote.Original);

        // Pending (el QR caducó y volvió atrás, o el aspirante está eligiendo otro método) o Processing
        // (este QR, otro intento o una cripto esperando): el alta sigue sin activar y este pago la activa.
        // Completed (u otro): ya se cobró por otro medio. Doble cobro, a devolver a mano.
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Processing))
            return await ReviewAsync(payment, "ORDER_ALREADY_SETTLED", now, remote.Original);

        // El pago, el pedido y todo lo que guarda la activación van en UNA transacción: o entra todo o
        // nada. Cualquier excepción dentro deja el contexto limpio antes de salir (el barrido o un
        // reintento pueden reutilizarlo y no deben guardar Paid/Consumed/PaymentHistory sin activación).
        // Una DbUpdateConcurrencyException sale de aquí y SettleAsync vuelve a leer.
        string? failureCode = null;
        Result<MemberProfile>? activation = null;
        await using (var tx = await _db.Database.BeginTransactionAsync(none))
        {
            try
            {
                // 1) El PAGO se reclama de forma atómica antes que nada: si otro camino (webhook, pantalla,
                //    barrido, admin) ya lo liquidó, este no hace nada más. Es lo que impide que el que llega
                //    segundo caiga en el pedido ya Completed y marque a revisar un pago legítimo.
                if (!await TryClaimPaymentAsync(payment, paidAt, remote.Original, now))
                {
                    await tx.RollbackAsync(none);
                    _db.ChangeTracker.Clear();
                    await EnsureUserActiveAsync(payment.MemberId);
                    return EntraxSettlementOutcome.AlreadyResolved;
                }

                _db.PaymentHistories.Add(new PaymentHistory
                {
                    OrderId              = order.Id,
                    MemberId             = order.MemberId,
                    Amount               = remote.Original,
                    GatewayName          = EntraxGateway.Name,
                    GatewayTransactionId = payment.ExternalPaymentId,
                    TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
                    ProcessedAt          = now,
                    CreationDate         = now,
                    CreatedBy            = Actor,
                    LastUpdateDate       = now,
                    LastUpdateBy         = Actor
                });

                await CancelWaitingCryptoAsync(order.Id, payment.ExternalPaymentId!, now);

                // 2) El PEDIDO, también atómico: SignupOrderClaim dentro de la activación.
                activation = await _activation.ActivatePendingSignupAsync(order, now, none);
                if (activation.IsSuccess)
                {
                    await _db.SaveChangesAsync(none);
                    await tx.CommitAsync(none);
                }
                else
                {
                    // Miembro o suscripción que no están, o el pedido ya activado por otro pago
                    // (ORDER_ALREADY_SETTLED): no se activó nada.
                    await tx.RollbackAsync(none);
                    failureCode = activation.ErrorCode ?? "ACTIVATION_FAILED";
                }
            }
            catch (Exception ex) when (ex is not DbUpdateConcurrencyException && IsOrderAlreadyConsumed(ex))
            {
                // Otro pago de Entrax del mismo pedido quedó Consumed antes: doble cobro.
                await tx.RollbackAsync(none);
                failureCode = SignupOrderClaim.AlreadySettledCode;
            }
            catch
            {
                // La transacción se deshace al soltarla; el contexto se limpia aquí.
                _db.ChangeTracker.Clear();
                throw;
            }
        }

        if (failureCode is not null)
        {
            // Se descarta lo marcado y se relee el pago. Si otro camino lo liquidó entre tanto (por eso
            // este perdió en el pedido o ya no encontró la suscripción pendiente), está Paid: es el mismo
            // pago ya resuelto, no un doble cobro.
            _db.ChangeTracker.Clear();
            var again = await _db.EntraxPayments.FirstAsync(p => p.Id == payment.Id, none);
            if (again.Status is EntraxPaymentStatus.Paid or EntraxPaymentStatus.ToReview)
            {
                if (again.Status == EntraxPaymentStatus.Paid)
                    await EnsureUserActiveAsync(again.MemberId);
                return EntraxSettlementOutcome.AlreadyResolved;
            }

            again.PaidAt        = paidAt;
            again.LastCheckedAt = now;
            return await ReviewAsync(again, failureCode, now, remote.Original);
        }

        await SignupUserActivation.ActivateAsync(_users, activation!.Value!.Email);

        _logger.LogInformation("Entrax: alta {OrderId} activada con el pago {ExternalPaymentId} ({Source}).",
            order.Id, payment.ExternalPaymentId, source);
        return EntraxSettlementOutcome.Activated;
    }

    /// <summary>
    /// Pasa el pago a Paid/Consumed con un UPDATE condicional (no estaba ya Paid ni ToReview), dentro de
    /// la transacción de la liquidación: el segundo que llegue espera al bloqueo de la fila y, cuando el
    /// primero confirma, ya no la encuentra. Devuelve false si otro camino ya lo había liquidado.
    /// </summary>
    private async Task<bool> TryClaimPaymentAsync(EntraxPayment payment, DateTime paidAt, decimal paidUsd, DateTime now)
    {
        var none = CancellationToken.None;

        if (!_db.Database.IsRelational())
        {
            // EF InMemory (pruebas) no tiene UPDATE condicional: se mira lo que hay guardado ahora.
            var current = await _db.EntraxPayments.AsNoTracking()
                .Where(p => p.Id == payment.Id).Select(p => p.Status).FirstAsync(none);
            if (current is EntraxPaymentStatus.Paid or EntraxPaymentStatus.ToReview)
                return false;

            payment.Status         = EntraxPaymentStatus.Paid;
            payment.Consumed       = true;
            payment.ActivatedAt    = now;
            payment.LastUpdateDate = now;
            payment.LastUpdateBy   = Actor;
            return true;
        }

        var affected = await _db.EntraxPayments
            .Where(p => p.Id == payment.Id
                     && p.Status != EntraxPaymentStatus.Paid
                     && p.Status != EntraxPaymentStatus.ToReview)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, EntraxPaymentStatus.Paid)
                .SetProperty(p => p.Consumed, true)
                .SetProperty(p => p.ActivatedAt, now)
                .SetProperty(p => p.PaidAt, paidAt)
                .SetProperty(p => p.PaidAmountUsd, paidUsd)
                .SetProperty(p => p.LastUpdateDate, now)
                .SetProperty(p => p.LastUpdateBy, Actor), none);

        if (affected != 1)
            return false;

        // La fila seguida, con lo que acaba de escribirse y su RowVersion nuevo.
        await _db.Entry(payment).ReloadAsync(none);
        return true;
    }

    private async Task<EntraxSettlementOutcome> CloseAsync(
        EntraxPayment payment, EntraxPaymentStatus to, string? code, DateTime now)
    {
        var none = CancellationToken.None;

        if (payment.Status != to)
        {
            payment.Status         = to;
            payment.FailureCode    = code ?? payment.FailureCode;
            payment.LastUpdateDate = now;
            payment.LastUpdateBy   = Actor;

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, none);
            if (order is not null)
                await ReleaseOrderIfIdleAsync(order, payment.Id, now);
        }

        await _db.SaveChangesAsync(none);
        return to == EntraxPaymentStatus.Failed ? EntraxSettlementOutcome.Failed : EntraxSettlementOutcome.Expired;
    }

    /// <summary>
    /// El pedido vuelve a Pending —y el aspirante puede pedir otro QR o elegir otro método— solo si
    /// nada más lo está esperando: ni otro QR de Entrax vivo ni una cripto pendiente.
    /// </summary>
    private async Task ReleaseOrderIfIdleAsync(Orders order, string exceptPaymentId, DateTime now)
    {
        var none = CancellationToken.None;
        if (order.Status != OrderStatus.Processing) return;

        var otherEntrax = await _db.EntraxPayments.AnyAsync(p => p.OrderId == order.Id && p.Id != exceptPaymentId
            && p.Status == EntraxPaymentStatus.AwaitingPayment, none);
        var crypto = await _db.CryptoPaymentConfirmations.AnyAsync(c => c.OrderId == order.Id
            && c.Status == CryptoPaymentConfirmationStatus.AwaitingPayment, none);
        if (otherEntrax || crypto) return;

        order.Status         = OrderStatus.Pending;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = Actor;
    }

    private async Task CancelWaitingCryptoAsync(string orderId, string externalPaymentId, DateTime now)
    {
        var waiting = await _db.CryptoPaymentConfirmations
            .Where(c => c.OrderId == orderId && c.Purpose == CryptoPaymentPurpose.Signup
                     && c.Status == CryptoPaymentConfirmationStatus.AwaitingPayment)
            .ToListAsync(CancellationToken.None);

        foreach (var c in waiting)
        {
            c.Status         = CryptoPaymentConfirmationStatus.Cancelled;
            c.Notes          = $"Paid via Entrax ({externalPaymentId}).";
            c.LastUpdateDate = now;
            c.LastUpdateBy   = Actor;
        }
    }

    private async Task<EntraxSettlementOutcome> ReviewAsync(EntraxPayment payment, string reason, DateTime now, decimal? paidUsd)
    {
        payment.Status         = EntraxPaymentStatus.ToReview;
        payment.FailureCode    = reason.Length > 60 ? reason[..60] : reason;
        payment.PaidAmountUsd  = paidUsd ?? payment.PaidAmountUsd;
        payment.LastUpdateDate = now;
        payment.LastUpdateBy   = Actor;
        await _db.SaveChangesAsync(CancellationToken.None);

        _logger.LogCritical(
            "Entrax: el pago {ExternalPaymentId} del alta {OrderId} queda A REVISAR ({Reason}). No se activó nada; " +
            "si hubo cobro hay que aplicarlo o devolverlo a mano.",
            payment.ExternalPaymentId, payment.OrderId, reason);
        return EntraxSettlementOutcome.ToReview;
    }

    private static bool IsOrderAlreadyConsumed(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e.Message.Contains(OrderConsumedIndex, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private async Task EnsureUserActiveAsync(string memberId)
    {
        var email = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => m.Email)
            .FirstOrDefaultAsync(CancellationToken.None);

        if (email is not null)
            await SignupUserActivation.ActivateAsync(_users, email);
    }
}
