using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>
/// "Pagar ahora" (Plan K, K14). El miembro sale del token (lo pone el controlador). La pantalla manda una
/// clave por intento: la misma clave devuelve el mismo pago (o el mismo fallo) sin mover dinero otra vez.
/// Un intento que no llegó a crear el pago (sin token, sin saldo) no ocupa la clave: se puede repetir.
/// </summary>
public sealed partial class AnnualFeeChargeService
{
    public Task<bool> HasPaymentAsync(string memberId, string? idempotencyKey, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(memberId) || string.IsNullOrWhiteSpace(idempotencyKey)
            ? Task.FromResult(false)
            : _db.AnnualFeePayments.AsNoTracking()
                 .AnyAsync(p => p.MemberId == memberId && p.IdempotencyKey == idempotencyKey, ct);

    public async Task<Result<AnnualFeePaymentResultDto>> PayNowAsync(AnnualFeePayNowInput input, CancellationToken ct = default)
    {
        var req = input.Request;

        // Solo por nombre y sin tarjeta: la tarjeta queda para el Plan I y nunca se cobra con el stub.
        var methodName = AnnualFeeMethods.Normalize(req.Method);
        if (methodName is null)
            return PayFail(AnnualFeeErrors.MethodNotSupported, "This payment method is not available for the annual fee.");
        if (!TokenPurchaseRules.IsValidIdempotencyKey(req.IdempotencyKey))
            return PayFail(AnnualFeeErrors.IdempotencyKeyRequired, "A valid idempotency key is required.");

        var method = Enum.Parse<AnnualFeePaymentMethod>(methodName);
        var replay = await ReplayAsync(input.MemberId, req.IdempotencyKey, method, ct);
        if (replay is not null) return replay;

        var member = await _db.MemberProfiles.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == input.MemberId, ct);
        if (member is null || member.MemberType != MemberType.Ambassador)
            return PayFail(AnnualFeeErrors.NotApplicable, "Only Ambassadors have a Biz Center annual fee.");

        var state = await _enrollment.EnsureForMemberAsync(input.MemberId, ct);
        if (state is null)
            return PayFail(AnnualFeeErrors.PlanNotConfigured, "The annual fee is not configured.");

        var refusal = AnnualFeeEligibility.ForPayNow(
            await AnnualFeeMemberQueries.FactsAsync(_db, input.MemberId, ct), state.NextBillingDate, _clock.Now);
        if (refusal is not null)
            return PayFail(refusal, "The annual fee cannot be paid right now.");

        var price = await AnnualFeePlanQueries.PriceAsync(_db, state.Plan!, ct);
        if (price is null)
            return PayFail(AnnualFeeErrors.PriceNotSet, "The annual fee has no price.");

        Settled settled;
        try
        {
            settled = method switch
            {
                AnnualFeePaymentMethod.Token =>
                    await SettleWithTokenAsync(state, price, AnnualFeePaymentSource.PayNow, req.IdempotencyKey, input.Actor, DueRule.PayAhead, ct),
                AnnualFeePaymentMethod.CommissionBalance =>
                    await SettleWithCommissionsAsync(state, price, AnnualFeePaymentSource.PayNow, req.IdempotencyKey, input.Actor, DueRule.PayAhead, ct),
                _ =>
                    await SettleWithEWalletAsync(state, price, AnnualFeePaymentSource.PayNow, req.IdempotencyKey, input.Actor, DueRule.PayAhead, ct)
            };
        }
        catch (MemberFundsBusyException)
        {
            return PayFail(AnnualFeeErrors.Busy, "Another payment for this account is in progress. Try again in a moment.");
        }

        switch (settled.Kind)
        {
            case SettleKind.Paid:
                return Result<AnnualFeePaymentResultDto>.Success(ToResult(settled.Payment!, settled.Order!, state.NextBillingDate));
            case SettleKind.Review:
                return PayFail(AnnualFeeErrors.NeedsReview, "We couldn't confirm the eWallet charge. It will be reviewed; do not retry.");
        }

        if (settled.Code == ReplayCode)
            return await ReplayAsync(input.MemberId, req.IdempotencyKey, method, ct)
                   ?? PayFail(AnnualFeeErrors.InProgress, "This payment is still being processed.");

        return PayFail(settled.Code ?? AnnualFeeErrors.NoFunds, MessageFor(settled.Code));
    }

    /// <summary>
    /// La misma clave del mismo miembro: Completed → el pago; Pending → en curso; NeedsReconciliation →
    /// en revisión; Failed → el mismo fallo (la pantalla genera otra clave). Con otro método no es un
    /// reintento: IDEMPOTENCY_KEY_REUSED.
    /// </summary>
    private async Task<Result<AnnualFeePaymentResultDto>?> ReplayAsync(
        string memberId, string key, AnnualFeePaymentMethod method, CancellationToken ct)
    {
        var existing = await _db.AnnualFeePayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MemberId == memberId && p.IdempotencyKey == key, ct);
        if (existing is null) return null;

        if (existing.Method != method)
            return PayFail(AnnualFeeErrors.IdempotencyKeyReused,
                "This idempotency key was already used for a different payment. Start a new one.");

        switch (existing.Status)
        {
            case AnnualFeePaymentStatus.Pending:
                return PayFail(AnnualFeeErrors.InProgress, "This payment is still being processed.");
            case AnnualFeePaymentStatus.NeedsReconciliation:
                return PayFail(AnnualFeeErrors.NeedsReview, "This payment is being reviewed; do not retry.");
            case AnnualFeePaymentStatus.Failed:
                return PayFail(existing.FailureCode ?? AnnualFeeErrors.NoFunds, "This payment already failed. Start a new one.");
        }

        var order   = await _db.Orders.AsNoTracking().FirstAsync(o => o.Id == existing.OrderId, ct);
        var nextDue = await _db.SubscriptionBillingStates.AsNoTracking()
            .Where(s => s.Id == existing.SubscriptionBillingStateId)
            .Select(s => s.NextBillingDate)
            .FirstAsync(ct);
        return Result<AnnualFeePaymentResultDto>.Success(ToResult(existing, order, nextDue));
    }

    private static AnnualFeePaymentResultDto ToResult(AnnualFeePayment payment, Orders order, DateTime nextDue) => new()
    {
        PaymentId    = payment.Id,
        OrderId      = order.Id,
        OrderNo      = order.OrderNo,
        Status       = payment.Status.ToString(),
        Method       = payment.Method.ToString(),
        Amount       = payment.Amount,
        CycleDueDate = payment.CycleDueDate,
        NextDueDate  = nextDue.Date,
        TokenCode    = payment.TokenCode
    };

    /// <summary>Mensajes fijos: el texto de i-Payout se queda en el log y en las notas, nunca en la respuesta.</summary>
    private static string MessageFor(string? code) => code switch
    {
        AnnualFeeErrors.NoFeeToken                    => "You have no annual-fee token available.",
        AnnualFeeErrors.InsufficientCommissionBalance => "Your commission balance is not enough for the annual fee.",
        AnnualFeeErrors.InsufficientEWalletBalance    => "Your eWallet balance is not enough for the annual fee.",
        AnnualFeeErrors.EWalletNotFound               => "You have no approved eWallet account.",
        AnnualFeeErrors.EWalletUnavailable            => "We couldn't reach your eWallet right now. Try again later.",
        AnnualFeeErrors.EWalletRejected               => "Your eWallet declined the charge.",
        AnnualFeeErrors.AlreadyPaid                   => "The annual fee for this cycle is already paid.",
        AnnualFeeErrors.InProgress                    => "This payment is still being processed.",
        AnnualFeeErrors.NeedsReview                   => "A payment for this cycle is being reviewed; do not retry.",
        AnnualFeeErrors.NotDue                        => "The annual fee is not due yet.",
        AnnualFeeErrors.MembershipOnHold              => "Your membership is on hold: there is no annual fee to pay right now.",
        AnnualFeeErrors.Busy                          => "Another payment for this account is in progress. Try again in a moment.",
        _                                             => "The annual fee could not be paid."
    };

    private static Result<AnnualFeePaymentResultDto> PayFail(string code, string message) =>
        Result<AnnualFeePaymentResultDto>.Failure(code, message);
}
