using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Un cobro de la cuota anual (Plan K, K10). Es a la vez la IDEMPOTENCIA de "Pagar ahora" ((MemberId,
/// IdempotencyKey) único) y la garantía contra el doble cobro: (SubscriptionBillingStateId,
/// CycleDueDate) es único entre los pagos vivos (Pending, Completed, NeedsReconciliation). Su Id es
/// también la referencia del débito en i-Payout.
/// </summary>
public class AnnualFeePayment : AuditChangesStringKey
{
    public const int FailureCodeMaxLength = 64;
    public const int NotesMaxLength       = 500;
    public const int TokenCodeMaxLength   = 64;

    private string? _failureCode;
    private string? _notes;

    public string MemberId { get; set; } = string.Empty;
    public string SubscriptionBillingStateId { get; set; } = string.Empty;

    /// <summary>El vencimiento del ciclo que paga (fecha, sin hora). Identifica el ciclo.</summary>
    public DateTime CycleDueDate { get; set; }

    /// <summary>Lo cobrado: el precio de la cuota con saldo; 0 con token (el dinero se movió al comprarlo).</summary>
    public decimal Amount { get; set; }

    public AnnualFeePaymentMethod Method { get; set; }
    public AnnualFeePaymentSource Source { get; set; }
    public AnnualFeePaymentStatus Status { get; set; } = AnnualFeePaymentStatus.Pending;
    public string OrderId { get; set; } = string.Empty;
    public string? PaymentHistoryId { get; set; }

    /// <summary>La instancia de token (con código) que se gastó, si se pagó con token.</summary>
    public long? TokenTransactionId { get; set; }
    public string? TokenCode { get; set; }

    /// <summary>La clave de la pantalla en "Pagar ahora". Null en el cobro automático.</summary>
    public string? IdempotencyKey { get; set; }

    public string? FailureCode
    {
        get => _failureCode;
        set => _failureCode = Clip(value, FailureCodeMaxLength);
    }

    public string? Notes
    {
        get => _notes;
        set => _notes = Clip(value, NotesMaxLength);
    }

    public DateTime? CompletedAt { get; set; }

    /// <summary>¿Ocupa su ciclo? No se mapea.</summary>
    public bool IsLive => Status is AnnualFeePaymentStatus.Pending
                                 or AnnualFeePaymentStatus.Completed
                                 or AnnualFeePaymentStatus.NeedsReconciliation;

    public void Complete(string? paymentHistoryId, DateTime now, string actor)
    {
        EnsureOpen("completar");
        Status           = AnnualFeePaymentStatus.Completed;
        PaymentHistoryId = paymentHistoryId;
        CompletedAt      = now;
        Touch(now, actor);
    }

    public void CompleteWithToken(long tokenTransactionId, string tokenCode, DateTime now, string actor)
    {
        EnsureOpen("completar con token");
        Status             = AnnualFeePaymentStatus.Completed;
        TokenTransactionId = tokenTransactionId;
        TokenCode          = Clip(tokenCode, TokenCodeMaxLength);
        CompletedAt        = now;
        Touch(now, actor);
    }

    /// <summary>No entró: un rechazo del eWallet, o tesorería lo da por no cobrado.</summary>
    public void Fail(string failureCode, DateTime now, string actor)
    {
        EnsureOpen("marcar fallido");
        Status      = AnnualFeePaymentStatus.Failed;
        FailureCode = failureCode;
        Touch(now, actor);
    }

    /// <summary>Resultado dudoso: ni avanza el ciclo ni se reintenta; lo cierra tesorería.</summary>
    public void MarkForReconciliation(string reasonCode, string? detail, DateTime now, string actor)
    {
        if (Status != AnnualFeePaymentStatus.Pending)
            throw new InvalidOperationException($"El pago {Id} está en {Status} y no puede pasar a conciliación.");

        Status      = AnnualFeePaymentStatus.NeedsReconciliation;
        FailureCode = reasonCode;
        Notes       = detail;
        Touch(now, actor);
    }

    private void EnsureOpen(string what)
    {
        if (Status is not (AnnualFeePaymentStatus.Pending or AnnualFeePaymentStatus.NeedsReconciliation))
            throw new InvalidOperationException($"El pago {Id} está en {Status} y no se puede {what}.");
    }

    private void Touch(DateTime now, string actor)
    {
        LastUpdateDate = now;
        LastUpdateBy   = actor;
    }

    private static string? Clip(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
