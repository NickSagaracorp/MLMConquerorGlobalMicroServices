namespace MLMConquerorGlobalEdition.Domain.Enums;

public enum TokenPurchaseStatus
{
    /// <summary>Cupo reservado; el cobro está en curso.</summary>
    Pending = 1,

    /// <summary>Cobrado y con los tokens emitidos.</summary>
    Completed = 2,

    /// <summary>No se cobró. No emitió nada y libera el cupo.</summary>
    Failed = 3,

    /// <summary>Pago en cripto esperando que un administrador confirme la transferencia.</summary>
    AwaitingCryptoConfirmation = 4,

    /// <summary>
    /// No se sabe si el dinero salió (débito eWallet en PROCESSING/PENDING o sin respuesta clara).
    /// Sin tokens, con el cupo reservado, hasta que tesorería la cierre.
    /// </summary>
    NeedsReconciliation = 5,

    /// <summary>Cripto sin pagar que el propio miembro retiró. Sin tokens; libera el cupo.</summary>
    Cancelled = 6,

    /// <summary>
    /// Cripto que un administrador rechazó (no llegó la transferencia, llegó otro importe, era
    /// fraude). Sin tokens; libera el cupo.
    /// </summary>
    Rejected = 7,

    /// <summary>
    /// Cripto que nadie confirmó dentro del plazo (Billing:TokenPurchase:CryptoExpiryDays). Sin
    /// tokens; libera el cupo. Si la transferencia llega después, tesorería la trata a mano: la
    /// compra caducada ya no se puede confirmar.
    /// </summary>
    Expired = 8
}
