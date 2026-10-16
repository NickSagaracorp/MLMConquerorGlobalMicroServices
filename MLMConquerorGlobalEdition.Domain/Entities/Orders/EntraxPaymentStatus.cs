namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Estado de un pago del alta con Entrax (SBP, rublos). Los nombres son los de
/// <c>SharedKernel.Billing.EntraxSignupStatuses</c>: la pantalla los recibe tal cual.
/// </summary>
public enum EntraxPaymentStatus
{
    /// <summary>QR emitido (o a punto de emitirse si aún no hay ExternalPaymentId); esperando el pago.</summary>
    AwaitingPayment = 1,

    /// <summary>Entrax dijo "paid", el importe cuadra y el alta se activó con este pago.</summary>
    Paid = 2,

    /// <summary>Entrax dijo "failed", o el QR no llegó a emitirse. El pedido vuelve a Pending.</summary>
    Failed = 3,

    /// <summary>Entrax dijo "expired". El pedido vuelve a Pending; si luego llega "paid", se re-evalúa.</summary>
    Expired = 4,

    /// <summary>
    /// Hubo (o pudo haber) cobro pero NO se aplicó: importe o moneda que no cuadran, pedido ya activado
    /// por otro medio, datos del proveedor que no coinciden. Se mira y se devuelve a mano.
    /// </summary>
    ToReview = 5,

    /// <summary>
    /// El alta se activó con otro pago (otro QR de Entrax) mientras este QR seguía vivo. La pantalla lo
    /// oculta; el barrido lo sigue mirando mientras pueda pagarse, y si alguien lo paga es un doble cobro
    /// (ToReview con ORDER_ALREADY_SETTLED).
    /// </summary>
    Superseded = 6
}
