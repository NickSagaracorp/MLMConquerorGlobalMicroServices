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
    NeedsReconciliation = 5
}
