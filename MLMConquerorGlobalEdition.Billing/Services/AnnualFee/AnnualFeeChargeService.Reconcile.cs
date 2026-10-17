using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>Conciliación y barrido de los pagos de cuota dudosos (Plan K, K11).</summary>
public sealed partial class AnnualFeeChargeService
{
    public async Task<Result<AnnualFeePaymentResultDto>> ReconcileAsync(
        string paymentId, ReconcileAnnualFeePaymentRequest request, string actor, CancellationToken ct = default)
    {
        var memberId = await _db.AnnualFeePayments.AsNoTracking()
            .Where(p => p.Id == paymentId)
            .Select(p => p.MemberId)
            .FirstOrDefaultAsync(ct);
        if (memberId is null)
            return PayFail(AnnualFeeErrors.PaymentNotFound, "Annual-fee payment not found.");

        // Revisión M-4: topes antes de tocar nada.
        var reference = string.IsNullOrWhiteSpace(request.GatewayReference) ? null : request.GatewayReference.Trim();
        if (reference is { Length: > AnnualFeeRules.GatewayReferenceMaxLength })
            return PayFail(AnnualFeeErrors.GatewayReferenceTooLong,
                $"The gateway reference can have at most {AnnualFeeRules.GatewayReferenceMaxLength} characters.");
        var userNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (userNotes is { Length: > AnnualFeeRules.ReconcileNotesMaxLength })
            return PayFail(AnnualFeeErrors.ReconcileNotesTooLong,
                $"The notes can have at most {AnnualFeeRules.ReconcileNotesMaxLength} characters.");

        try
        {
            var before = Tracked();
            await using var tx = await _gate.EnterAsync(memberId, ct);
            try
            {

                var payment = await _db.AnnualFeePayments.FirstAsync(p => p.Id == paymentId, ct);
                if (payment.Status != AnnualFeePaymentStatus.NeedsReconciliation)
                {
                    await SafeRollbackAsync(tx, memberId);
                    return PayFail(AnnualFeeErrors.NotUnderReview, "This payment is not under review.");
                }

                var order = await _db.Orders.FirstAsync(o => o.Id == payment.OrderId, ct);
                var state = await _db.SubscriptionBillingStates
                    .Include(s => s.Plan).ThenInclude(p => p!.PlanProducts)
                    .FirstAsync(s => s.Id == payment.SubscriptionBillingStateId, ct);
                var now = _clock.Now;
                string? warning = null;

                if (request.Charged)
                {
                    var history = AddEWalletPaymentHistory(order, payment.Amount, reference ?? payment.Id, actor, now);
                    payment.Complete(history.Id, now, actor);
                    MarkPaid(order, actor, now);

                    if (state.NextBillingDate.Date == payment.CycleDueDate.Date)
                    {
                        // El débito se hizo el día del pago, no hoy: el siguiente vencimiento se cuenta desde entonces.
                        var attemptIndex = AdvanceOnSuccess(state, payment.CycleDueDate, payment.CreationDate, now, actor);
                        var productId = state.Plan!.PlanProducts.OrderBy(pp => pp.Id).Select(pp => pp.ProductId).FirstOrDefault() ?? string.Empty;
                        AddAttempt(state, productId, RecurringFundingSource.EWallet, RecurringAttemptOutcome.Success, payment.Amount,
                            order.Id, history.Id, tokenTransactionId: null, commissionDebitId: null, failureReason: null, now, actor,
                            attemptIndex: attemptIndex);
                    }
                    else
                    {
                        // Revisión M-3: el ciclo ya lo pagó otra vía mientras este débito estaba en revisión.
                        // El dinero salió (se completa), el estado no se mueve, y es un posible doble cobro.
                        warning = AnnualFeeErrors.ReconciledCycleAlreadyAdvanced;
                        _logger.LogCritical(
                            "Cuota anual {PaymentId} de {MemberId}: conciliada como cobrada ({Reference}) pero el estado ya vence el {Due} y no el {Cycle}. " +
                            "Posible doble cobro: revisar y devolver si procede.",
                            payment.Id, memberId, reference, state.NextBillingDate, payment.CycleDueDate);
                    }
                }
                else
                {
                    payment.Fail(AnnualFeeErrors.NotCharged, now, actor);
                    Cancel(order, actor, now);
                }

                // Revisión M-7: auditoría explícita de la conciliación. Va delante en las notas para que, si
                // hay que recortar (500), se pierda lo más antiguo y no quién, cuándo, con qué referencia y qué
                // resultado. Quién y cuándo quedan también en LastUpdateBy y LastUpdateDate del pago.
                var audit = $"[reconciled {now:yyyy-MM-dd HH:mm} by {actor}: {(request.Charged ? "charged" : "not charged")}" +
                            $", ref {reference ?? "-"}{(warning is null ? "" : ", " + warning)}]";
                payment.Notes = string.Join(" | ", new[] { audit, userNotes, payment.Notes }.Where(x => !string.IsNullOrWhiteSpace(x)));
                _logger.LogWarning(
                    "Cuota anual {PaymentId} de {MemberId} conciliada por {Actor}: cobrado={Charged}, referencia={Reference}, aviso={Warning}.",
                    payment.Id, memberId, actor, request.Charged, reference, warning);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                var dto = ToResult(payment, order, state.NextBillingDate);
                dto.Warning = warning;
                return Result<AnnualFeePaymentResultDto>.Success(dto);
            }
            catch (Exception)
            {
                // Revisión C-1: nada a medias en el contexto; se relanza.
                await AbortUnderGateAsync(tx, memberId, before);
                throw;
            }
        }
        catch (MemberFundsBusyException)
        {
            return PayFail(AnnualFeeErrors.Busy, "Another payment for this account is in progress. Try again in a moment.");
        }
    }

    public async Task<int> MoveStalePendingToReconciliationAsync(int minutes, string actor, CancellationToken ct = default)
    {
        var now    = _clock.Now;
        var cutoff = now.AddMinutes(-minutes);
        var stale  = await _db.AnnualFeePayments
            .Where(p => p.Status == AnnualFeePaymentStatus.Pending && p.CreationDate <= cutoff)
            .ToListAsync(ct);

        foreach (var payment in stale)
            payment.MarkForReconciliation("STALE_PENDING",
                $"Pending for {minutes}+ minutes: the process stopped between the charge and its record.", now, actor);

        try
        {
            await _db.SaveChangesAsync(ct);
            return stale.Count;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Otro proceso lo cerró a la vez (la fase 2 terminó justo ahora): se deja para la próxima pasada.
            _logger.LogWarning(ex, "Barrido de pagos de cuota: alguno cambió mientras tanto; se mira en la próxima pasada.");
            return 0;
        }
    }
}
