using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

/// <summary>
/// Una compra de tokens desde el BizCenter. Es a la vez la RESERVA de cupo del mes (mientras está
/// en vuelo cuenta contra el tope aunque no haya tokens), la IDEMPOTENCIA ((MemberId,
/// IdempotencyKey) es único) y el VÍNCULO entre pedido, pago y códigos emitidos
/// (TokenTransaction.TokenPurchaseId). Su Id es también el MerchantReferenceID del débito eWallet.
///
/// No reembolsable: Completed no tiene salida. Una compra en cripto sin pagar sí la tiene: el
/// miembro la cancela, un administrador la rechaza o caduca; las tres liberan el cupo. Solo la
/// caducada se puede reabrir (la transferencia llegó tarde) y confirmar, si su mes aún tiene cupo.
/// </summary>
public class TokenPurchase : AuditChangesStringKey
{
    public const int NotesMaxLength       = 500;
    public const int FailureCodeMaxLength = 64;

    public const string CancelledByMemberCode = "CANCELLED_BY_MEMBER";
    public const string RejectedByAdminCode   = "REJECTED_BY_ADMIN";
    public const string CryptoExpiredCode     = "CRYPTO_PAYMENT_EXPIRED";

    private string? _failureCode;
    private string? _notes;

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

    /// <summary>
    /// yyyyMM del mes al que se imputa la compra (hora de servidor). Se fija al reservar y no cambia:
    /// los tokens que emita después cuentan en este mes, no en el de la emisión.
    /// </summary>
    public int PeriodKey { get; set; }

    public string? CreditCardId { get; set; }
    public string? PaymentHistoryId { get; set; }

    /// <summary>
    /// Se recorta aquí a la longitud de la columna: el código puede venir del gateway y un texto largo
    /// haría fallar el SaveChanges que deja constancia de un cobro que quizá ya ocurrió.
    /// </summary>
    public string? FailureCode
    {
        get => _failureCode;
        set => _failureCode = Clip(value, FailureCodeMaxLength);
    }

    /// <summary>Detalle para tesorería: por qué quedó a conciliar o cómo se cerró. Recortado como FailureCode.</summary>
    public string? Notes
    {
        get => _notes;
        set => _notes = Clip(value, NotesMaxLength);
    }

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
        Touch(now, actor);
    }

    /// <summary>Falla lo que se intentó cobrar y no entró, o lo que tesorería da por no cobrado.</summary>
    public void Fail(string failureCode, DateTime now, string actor)
    {
        if (Status is not (TokenPurchaseStatus.Pending or TokenPurchaseStatus.NeedsReconciliation))
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no puede marcarse fallida.");

        Status      = TokenPurchaseStatus.Failed;
        FailureCode = failureCode;
        Touch(now, actor);
    }

    /// <summary>El cobro tuvo un resultado dudoso: ni tokens ni reintento, a revisión.</summary>
    public void MarkForReconciliation(string reasonCode, string? detail, DateTime now, string actor)
    {
        if (Status != TokenPurchaseStatus.Pending)
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no puede pasar a conciliación.");

        Status      = TokenPurchaseStatus.NeedsReconciliation;
        FailureCode = reasonCode;
        Notes       = detail;
        Touch(now, actor);
    }

    // ── Cripto sin pagar ──────────────────────────────────────────────────────
    // Sin estas salidas, una compra en cripto que nadie paga reservaría el cupo para siempre: el
    // miembro no podría volver a comprar ese mes y el panel acumularía pedidos muertos.

    /// <summary>El miembro retira su compra en cripto antes de que se confirme.</summary>
    public void CancelByMember(DateTime now, string actor)
    {
        EnsureAwaitingCrypto("cancelar");
        Status      = TokenPurchaseStatus.Cancelled;
        FailureCode = CancelledByMemberCode;
        Touch(now, actor);
    }

    /// <summary>Un administrador da por no recibida (o no aceptable) la transferencia.</summary>
    public void Reject(string? reason, DateTime now, string actor)
    {
        EnsureAwaitingCrypto("rechazar");
        Status      = TokenPurchaseStatus.Rejected;
        FailureCode = RejectedByAdminCode;
        Notes       = string.IsNullOrWhiteSpace(reason) ? Notes : reason.Trim();
        Touch(now, actor);
    }

    /// <summary>
    /// Nadie la confirmó dentro del plazo (Billing:TokenPurchase:CryptoExpiryDays, contado desde
    /// CreationDate en hora de servidor; lo filtra TokenPurchaseService.ExpireStaleCryptoAsync).
    /// </summary>
    public void Expire(DateTime now, string actor)
    {
        EnsureAwaitingCrypto("caducar");
        Status      = TokenPurchaseStatus.Expired;
        FailureCode = CryptoExpiredCode;
        Touch(now, actor);
    }

    /// <summary>
    /// La transferencia llegó después de caducar: un aprobador la reabre para confirmarla. Solo una
    /// CADUCADA; la cancelada por el miembro o la rechazada fueron decisiones de alguien y no se
    /// reabren. Conserva su PeriodKey: vuelve a ocupar el cupo de su mes, y quien reabre debe comprobar
    /// antes que cabe.
    /// </summary>
    public void ReopenExpired(DateTime now, string actor)
    {
        if (Status != TokenPurchaseStatus.Expired)
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no se puede reabrir: solo una compra en cripto caducada.");

        Status      = TokenPurchaseStatus.AwaitingCryptoConfirmation;
        FailureCode = null;
        Touch(now, actor);
    }

    private void EnsureAwaitingCrypto(string action)
    {
        if (Status != TokenPurchaseStatus.AwaitingCryptoConfirmation)
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no se puede {action}: solo una compra en cripto sin confirmar.");
    }

    private void Touch(DateTime now, string actor)
    {
        LastUpdateDate = now;
        LastUpdateBy   = actor;
    }

    private static string? Clip(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
