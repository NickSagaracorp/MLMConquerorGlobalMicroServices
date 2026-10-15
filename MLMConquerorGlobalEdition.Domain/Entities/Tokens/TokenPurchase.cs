using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

/// <summary>
/// Una compra de tokens desde el BizCenter. Es a la vez la RESERVA de cupo del mes (mientras está
/// en vuelo cuenta contra el tope aunque no haya tokens), la IDEMPOTENCIA ((MemberId,
/// IdempotencyKey) es único) y el VÍNCULO entre pedido, pago y códigos emitidos
/// (TokenTransaction.TokenPurchaseId). Su Id es también el MerchantReferenceID del débito eWallet.
///
/// No reembolsable: Completed no tiene salida.
/// </summary>
public class TokenPurchase : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public int TokenTypeId { get; set; }

    /// <summary>Productos otorgados en el momento de la compra, separados por coma. De ahí salió el precio.</summary>
    public string GrantedProductIds { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public TokenPaymentMethod PaymentMethod { get; set; }
    public TokenPurchaseStatus Status { get; set; } = TokenPurchaseStatus.Pending;
    public string OrderId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>yyyyMM del mes al que se imputa la reserva (hora de servidor).</summary>
    public int PeriodKey { get; set; }

    public string? CreditCardId { get; set; }
    public string? PaymentHistoryId { get; set; }
    public string? FailureCode { get; set; }

    /// <summary>Detalle para tesorería: por qué quedó a conciliar o cómo se cerró.</summary>
    public string? Notes { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>¿Ocupa cupo del mes sin haber emitido? No se mapea.</summary>
    public bool IsReservation => Status is TokenPurchaseStatus.Pending
                                        or TokenPurchaseStatus.AwaitingCryptoConfirmation
                                        or TokenPurchaseStatus.NeedsReconciliation;

    public void Complete(string paymentHistoryId, DateTime now, string actor)
    {
        if (!IsReservation)
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no se puede completar.");

        Status           = TokenPurchaseStatus.Completed;
        PaymentHistoryId = paymentHistoryId;
        CompletedAt      = now;
        LastUpdateDate   = now;
        LastUpdateBy     = actor;
    }

    /// <summary>Falla lo que se intentó cobrar y no entró, o lo que tesorería da por no cobrado.</summary>
    public void Fail(string failureCode, DateTime now, string actor)
    {
        if (Status is not (TokenPurchaseStatus.Pending or TokenPurchaseStatus.NeedsReconciliation))
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no puede marcarse fallida.");

        Status         = TokenPurchaseStatus.Failed;
        FailureCode    = failureCode;
        LastUpdateDate = now;
        LastUpdateBy   = actor;
    }

    /// <summary>El cobro tuvo un resultado dudoso: ni tokens ni reintento, a revisión.</summary>
    public void MarkForReconciliation(string reasonCode, string? detail, DateTime now, string actor)
    {
        if (Status != TokenPurchaseStatus.Pending)
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no puede pasar a conciliación.");

        Status         = TokenPurchaseStatus.NeedsReconciliation;
        FailureCode    = reasonCode;
        Notes          = detail;
        LastUpdateDate = now;
        LastUpdateBy   = actor;
    }
}
