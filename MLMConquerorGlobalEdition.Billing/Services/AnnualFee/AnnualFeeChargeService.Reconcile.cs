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

                if (request.Charged)
                {
                    var history = AddEWalletPaymentHistory(order, payment.Amount,
                        string.IsNullOrWhiteSpace(request.GatewayReference) ? payment.Id : request.GatewayReference.Trim(), actor, now);
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
                        _logger.LogWarning("Cuota anual {PaymentId}: conciliada como cobrada, pero el estado ya vence el {Due} y no el {Cycle}; no se mueve.",
                            payment.Id, state.NextBillingDate, payment.CycleDueDate);
                    }
                }
                else
                {
                    payment.Fail(AnnualFeeErrors.NotCharged, now, actor);
                    Cancel(order, actor, now);
                }

                payment.Notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? payment.Notes
                    : string.IsNullOrWhiteSpace(payment.Notes) ? request.Notes.Trim() : $"{payment.Notes} | {request.Notes.Trim()}";

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Result<AnnualFeePaymentResultDto>.Success(ToResult(payment, order, state.NextBillingDate));
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
