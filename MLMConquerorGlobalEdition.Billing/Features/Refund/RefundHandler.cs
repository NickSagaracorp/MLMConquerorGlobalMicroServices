using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Billing;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.Refund;

public class RefundHandler : IRequestHandler<RefundCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IGatewayResolver _gatewayResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly int _lockTimeoutMs;

    /// <summary>
    /// Revision de Q3 (I2): la pasarela devolvio el dinero y el pago y el pedido quedaron Refunded, pero la membresia no se
    /// pudo cancelar (candado de puntos ocupado o fallo al guardar). No se reintenta la devolucion: hay que cancelarla a mano.
    /// </summary>
    public const string MembershipNotCancelledCode = "REFUNDED_MEMBERSHIP_NOT_CANCELLED";

    /// <param name="lockTimeoutMs">Espera por el candado de puntos del miembro; solo lo cambian las pruebas.</param>
    public RefundHandler(
        AppDbContext db,
        IGatewayResolver gatewayResolver,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        int lockTimeoutMs = MemberPointsLocks.LockTimeoutMs)
    {
        _db = db;
        _gatewayResolver = gatewayResolver;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _lockTimeoutMs = lockTimeoutMs;
    }

    public async Task<Result<bool>> Handle(RefundCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // Load payment history
        var payment = await _db.PaymentHistories
            .FirstOrDefaultAsync(p => p.Id == req.PaymentHistoryId && !p.IsDeleted, ct);

        if (payment is null)
            return Result<bool>.Failure("PAYMENT_NOT_FOUND",
                $"Payment history '{req.PaymentHistoryId}' not found.");

        // Only Captured (successful) payments can be refunded
        if (payment.TransactionStatus != PaymentHistoryTransactionStatus.Captured)
            return Result<bool>.Failure("PAYMENT_NOT_REFUNDABLE",
                $"Payment cannot be refunded. Current status: {payment.TransactionStatus}.");

        // Parse gateway type from GatewayName
        if (!Enum.TryParse<WalletType>(payment.GatewayName, ignoreCase: true, out var walletType))
            return Result<bool>.Failure("GATEWAY_UNKNOWN",
                $"Cannot determine gateway for payment '{req.PaymentHistoryId}'. GatewayName: '{payment.GatewayName}'.");

        // Only Stripe and eWallet supported for refunds
        if (walletType != WalletType.Dwolla && walletType != WalletType.eWallet)
            return Result<bool>.Failure("REFUND_NOT_SUPPORTED",
                $"Refunds are not supported for gateway '{walletType}' in this version.");

        IGatewayService gateway;
        try
        {
            gateway = _gatewayResolver.Resolve(walletType);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure("GATEWAY_NOT_SUPPORTED", ex.Message);
        }

        var refundAmount = req.Amount ?? payment.Amount;

        if (refundAmount <= 0 || refundAmount > payment.Amount)
            return Result<bool>.Failure("INVALID_REFUND_AMOUNT",
                $"Refund amount must be between 0.01 and {payment.Amount:F2}.");

        var transactionId = payment.GatewayTransactionId ?? string.Empty;
        var refundResult = await gateway.RefundAsync(transactionId, refundAmount, ct);

        if (!refundResult.IsSuccess)
            return Result<bool>.Failure(refundResult.ErrorCode!, refundResult.Error!);

        // Revision de Q3 (I2): la pasarela ya devolvio el dinero. Primero se guardan el pago y el pedido como Refunded, en su
        // propio guardado y sin candado (no tocan puntos): si lo que sigue falla, un reintento ve el pago Refunded y no
        // vuelve a pedir la devolucion. Ningun candado de base se mantiene durante la llamada a la pasarela.
        var Now = _dateTime.Now;
        payment.TransactionStatus = PaymentHistoryTransactionStatus.Refunded;
        payment.FailureReason = req.Reason;
        payment.LastUpdateDate = Now;
        payment.LastUpdateBy = _currentUser.UserId;

        // Update linked order if present
        Orders? order = null;
        if (!string.IsNullOrWhiteSpace(payment.OrderId))
        {
            order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == payment.OrderId && !o.IsDeleted, ct);

            if (order is not null)
            {
                order.Status = OrderStatus.Refunded;
                order.LastUpdateDate = Now;
                order.LastUpdateBy = _currentUser.UserId;
            }
        }

        await _db.SaveChangesAsync(ct);

        // Plan Q (Q3, decidido por Nick): la devolucion TOTAL del pedido vigente de una membresia viva la cancela; el
        // interceptor resta sus puntos del miembro y de su linea. Una parcial (un ajuste de importe) no la toca. Segundo
        // guardado: la cancelacion y la parada de su cobro, juntas, bajo el candado de puntos del miembro, tomado antes de
        // leer la suscripcion (como la subida y la bajada); el guardado lo reutiliza y lo suelta al confirmar, y si no se
        // llega a guardar, el finally.
        if (order is null || !RefundedMembershipOrders.IsFullRefund(refundAmount, payment.Amount))
            return Result<bool>.Success(true);

        try
        {
            await MemberPointsLocks.AcquireAsync(_db, new[] { order.MemberId }, ct, _lockTimeoutMs);
            try
            {
                await RefundedMembershipOrders.CancelSubscriptionAsync(_db, order.Id, Now, _currentUser.UserId, ct);
                await _db.SaveChangesAsync(ct);
            }
            finally
            {
                await MemberPointsLocks.ReleaseAsync(_db);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or System.Data.Common.DbException)
        {
            return Result<bool>.Failure(MembershipNotCancelledCode,
                "The refund was made and recorded (payment and order are Refunded), but the membership could not be " +
                $"cancelled now ({ex.Message}). Do not refund again; cancel the membership manually.");
        }
        return Result<bool>.Success(true);
    }
}
