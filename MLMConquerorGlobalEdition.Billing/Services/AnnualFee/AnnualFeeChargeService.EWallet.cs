using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>
/// La cuota con eWallet (Plan K, K11), en dos fases como TokenPurchaseService.PayWithEWalletAsync:
/// (1) bajo el candado se reserva el ciclo (pago Pending, pedido Processing) y se confirma antes de tocar
/// i-Payout; (2) con el candado otra vez y sin cancelación se relee el saldo real, se debita, se
/// verifica por saldo y se cierra. Lo dudoso nunca se reintenta: queda NeedsReconciliation.
/// El saldo es el real de i-Payout (eWallet, no comisiones).
/// </summary>
public sealed partial class AnnualFeeChargeService
{
    private const string EWalletGatewayName = "eWallet";

    private async Task<Settled> SettleWithEWalletAsync(
        SubscriptionBillingState state, AnnualFeePrice price, AnnualFeePaymentSource source, string? key,
        string actor, DueRule rule, CancellationToken ct)
    {
        var userName = await _eWallet.UserNameOfAsync(state.MemberId, ct);
        if (userName is null) return Settled.NotPossible(AnnualFeeErrors.EWalletNotFound);

        var charge = EWalletFunds.ChargeAmount(price.Amount);

        // Comprobación temprana para no reservar en vano; la que cuenta se repite con el candado.
        var early = await SafeBalanceAsync(state.MemberId, userName, ct);
        if (!early.IsSuccess)     return Settled.NotPossible(AnnualFeeErrors.EWalletUnavailable);
        if (early.Value < charge) return Settled.NotPossible(AnnualFeeErrors.InsufficientEWalletBalance);

        // Fase 1 — reservar el ciclo.
        var now     = _clock.Now;
        var before1 = Tracked();
        Orders order;
        AnnualFeePayment payment;
        DateTime cycle;
        await using (var tx = await _gate.EnterAsync(state.MemberId, ct))
        {
            try
            {
                var blocked = await BlockedUnderGateAsync(state, key, rule, now, ct);
                if (blocked is not null)
                {
                    await SafeRollbackAsync(tx, state.MemberId);
                    return Settled.Blocked(blocked);
                }

                cycle   = state.NextBillingDate.Date;
                order   = NewOrder(state.MemberId, charge, cycle, actor, now);
                payment = NewPayment(state, cycle, charge, AnnualFeePaymentMethod.EWallet, source, key, order, actor, now);
                try
                {
                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
                {
                    return await BlockAfterCollisionAsync(tx, state.MemberId, key, before1, ex);
                }
            }
            catch (Exception)
            {
                // Revisión C-1: nada a medias en el contexto compartido; se relanza.
                await AbortUnderGateAsync(tx, state.MemberId, before1);
                throw;
            }
        }

        // Fase 2 — con el candado: ningún otro débito nuestro del mismo miembro se cruza con la
        // verificación por saldo. Sin cancelación: desde aquí puede moverse dinero. Lo cargado hasta aquí
        // está guardado (fase 1): si hay que rescatar, se relee y lo nuevo se suelta.
        var before2 = Tracked();
        IDbContextTransaction tx2;
        try
        {
            tx2 = await _gate.EnterAsync(state.MemberId, CancellationToken.None);
        }
        catch (MemberFundsBusyException)
        {
            return await AbandonAsync(order, payment, AnnualFeeErrors.Busy, actor, notPossible: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cuota anual {PaymentId}: no se pudo tomar el candado antes del débito eWallet.", payment.Id);
            return await AbandonAsync(order, payment, AnnualFeeErrors.Busy, actor, notPossible: false);
        }

        await using (tx2)
        {
            var before = await SafeBalanceAsync(state.MemberId, userName, CancellationToken.None);
            if (!before.IsSuccess || before.Value < charge)
            {
                await ReleaseOpenTransactionAsync();
                return await AbandonAsync(order, payment,
                    before.IsSuccess ? AnnualFeeErrors.InsufficientEWalletBalance : AnnualFeeErrors.EWalletUnavailable,
                    actor, notPossible: true);
            }

            EWalletDebitOutcome outcome;
            try
            {
                outcome = await _eWallet.DebitAsync(userName, charge, before.Value, payment.Id,
                    $"Biz Center annual fee {order.OrderNo}", CancellationToken.None);
            }
            catch (Exception ex)
            {
                // El débito pudo salir antes de la excepción: a conciliar, nunca liberar ni reintentar.
                _logger.LogError(ex, "Cuota anual {PaymentId}: el débito eWallet lanzó una excepción.", payment.Id);
                outcome = new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation, "EWALLET_OUTCOME_UNKNOWN", ex.GetType().Name);
            }

            if (outcome.Status != EWalletDebitStatus.Completed)
                _logger.LogWarning("Cuota anual {PaymentId}: débito eWallet de {User} {Status} {Code} {Detail}",
                    payment.Id, EWalletClient.MaskUserName(userName), outcome.Status, outcome.Code, outcome.Detail);

            var at = _clock.Now;
            try
            {
                switch (outcome.Status)
                {
                    case EWalletDebitStatus.Rejected:
                        payment.Fail(AnnualFeeErrors.EWalletRejected, at, actor);
                        payment.Notes = $"{outcome.Code}: {outcome.Detail}";
                        Cancel(order, actor, at);
                        await _db.SaveChangesAsync(CancellationToken.None);
                        await tx2.CommitAsync(CancellationToken.None);
                        return Settled.NotPossible(AnnualFeeErrors.EWalletRejected);

                    case EWalletDebitStatus.NeedsReconciliation:
                        payment.MarkForReconciliation(outcome.Code ?? "EWALLET_OUTCOME_UNKNOWN", outcome.Detail, at, actor);
                        MarkStateUnderReview(state, at, actor);
                        await _db.SaveChangesAsync(CancellationToken.None);
                        await tx2.CommitAsync(CancellationToken.None);
                        return Settled.Review(payment, order, AnnualFeeMethods.EWallet);
                }

                // Lo cobrado es el importe a centavos que mandó EWalletFunds.
                var history = AddEWalletPaymentHistory(order, charge, payment.Id, actor, at);
                payment.Complete(history.Id, at, actor);
                MarkPaid(order, actor, at);
                var attemptIndex = AdvanceOnSuccess(state, cycle, at, at, actor);
                AddAttempt(state, price.ProductId, RecurringFundingSource.EWallet, RecurringAttemptOutcome.Success, charge,
                    order.Id, history.Id, tokenTransactionId: null, commissionDebitId: null, failureReason: null, at, actor, attemptIndex: attemptIndex);
                await _db.SaveChangesAsync(CancellationToken.None);
                await tx2.CommitAsync(CancellationToken.None);
                return Settled.Paid(payment, order, AnnualFeeMethods.EWallet);
            }
            catch (Exception ex)
            {
                // No se pudo dejar constancia del resultado del débito: el dinero pudo salir.
                var code = outcome.Status switch
                {
                    EWalletDebitStatus.Completed => "EWALLET_CHARGED_NOT_RECORDED",
                    EWalletDebitStatus.Rejected  => "EWALLET_REJECTED_NOT_SAVED",
                    _                            => outcome.Code ?? "EWALLET_OUTCOME_UNKNOWN"
                };
                return await RescueToReconciliationAsync(payment.Id, order, code, ex.GetType().Name, actor, before2, ex);
            }
        }
    }

    /// <summary>El estado no avanza; la pasada de mañana verá el pago a conciliar y no hará nada.</summary>
    private static void MarkStateUnderReview(SubscriptionBillingState state, DateTime at, string actor)
    {
        state.LastAttemptAt      = at;
        state.LastAttemptOutcome = "NeedsReconciliation";
        state.LastFailureReason  = AnnualFeeErrors.NeedsReview;
        state.NextAttemptDate    = at.Date.AddDays(1);
        state.LastUpdateDate     = at;
        state.LastUpdateBy       = actor;
    }

    private PaymentHistory AddEWalletPaymentHistory(Orders order, decimal amount, string? gatewayTxId, string actor, DateTime now)
    {
        var history = new PaymentHistory
        {
            OrderId = order.Id, MemberId = order.MemberId, Amount = amount, GatewayName = EWalletGatewayName,
            GatewayTransactionId = gatewayTxId, TransactionStatus = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt = now, CreationDate = now, CreatedBy = actor, LastUpdateDate = now, LastUpdateBy = actor
        };
        _db.PaymentHistories.Add(history);
        return history;
    }

    /// <summary>
    /// Suelta la reserva cuando consta que NO se movió dinero. Si el guardado falla, el pago se queda
    /// Pending y el barrido lo pasa a conciliar (lado seguro: tesorería lo cierra como no cobrado).
    /// </summary>
    private async Task<Settled> AbandonAsync(Orders order, AnnualFeePayment payment, string code, string actor, bool notPossible)
    {
        try
        {
            var now = _clock.Now;
            payment.Fail(code, now, actor);
            Cancel(order, actor, now);
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cuota anual {PaymentId}: no se pudo soltar la reserva ({Code}); queda para el barrido de Pending.",
                payment.Id, code);
        }
        return notPossible ? Settled.NotPossible(code) : Settled.Blocked(code);
    }

    /// <summary>
    /// Último recurso cuando no se pudo guardar el resultado de un débito: se suelta la transacción, se
    /// olvida el contexto, se relee el pago y se deja a conciliar. Si tampoco se puede, Critical: lo
    /// recoge el barrido de Pending (AnnualFeeStalePendingJob). Nunca libera.
    /// </summary>
    private async Task<Settled> RescueToReconciliationAsync(
        string paymentId, Orders order, string code, string? detail, string actor, HashSet<object> before, Exception cause)
    {
        _logger.LogError(cause, "Cuota anual {PaymentId}: no se pudo guardar el resultado del débito ({Code}); se deja a conciliar.",
            paymentId, code);
        try
        {
            await ReleaseOpenTransactionAsync();
            // Sin limpiar el contexto entero (lo comparte el trabajador): se suelta lo añadido en la fase 2 y
            // se relee lo guardado en la fase 1 (pago Pending, pedido Processing, estado).
            await ForgetAfterRollbackAsync(before);

            var payment = await _db.AnnualFeePayments.FirstOrDefaultAsync(p => p.Id == paymentId, CancellationToken.None);
            if (payment is null)
            {
                _logger.LogCritical("Cuota anual {PaymentId}: no aparece al releerla tras un débito ({Code}). Revisar a mano.", paymentId, code);
                return Settled.Blocked(AnnualFeeErrors.NeedsReview);
            }

            if (payment.Status == AnnualFeePaymentStatus.Pending)
            {
                payment.MarkForReconciliation(code, detail, _clock.Now, actor);
                await _db.SaveChangesAsync(CancellationToken.None);
            }

            return Settled.Review(payment, order, AnnualFeeMethods.EWallet);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "Cuota anual {PaymentId} quedó Pending con un débito posiblemente hecho ({Code}) y no se pudo pasar a conciliar. " +
                "La recogerá el barrido de Pending; no liberar sin mirar i-Payout.", paymentId, code);
            return Settled.Blocked(AnnualFeeErrors.NeedsReview);
        }
    }

    /// <summary>El saldo de i-Payout sin que su texto llegue a nadie: un fallo es EWALLET_UNAVAILABLE.</summary>
    private async Task<Result<decimal>> SafeBalanceAsync(string memberId, string userName, CancellationToken ct)
    {
        try
        {
            var balance = await _eWallet.BalanceAsync(userName, ct);
            if (balance.IsSuccess) return balance;

            _logger.LogWarning("Saldo eWallet de {MemberId} ({User}) no disponible: {Code} {Error}",
                memberId, EWalletClient.MaskUserName(userName), balance.ErrorCode, balance.Error);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Saldo eWallet de {MemberId} ({User}): i-Payout no respondió.",
                memberId, EWalletClient.MaskUserName(userName));
        }

        return Result<decimal>.Failure(AnnualFeeErrors.EWalletUnavailable, "The eWallet balance is not available.");
    }

    /// <summary>Deshace y suelta la transacción abierta en el contexto, si la hay, sin lanzar.</summary>
    private async Task ReleaseOpenTransactionAsync()
    {
        if (_db.Database.CurrentTransaction is not { } open) return;
        try
        {
            await open.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo deshacer la transacción abierta; se descarta.");
        }
        finally
        {
            await open.DisposeAsync();
        }
    }
}
