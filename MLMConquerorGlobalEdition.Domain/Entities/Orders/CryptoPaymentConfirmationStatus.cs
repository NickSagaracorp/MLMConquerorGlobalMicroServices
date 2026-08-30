namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Estado de un cobro en cripto que se confirma a mano. No hay pasarela: el dinero llega por
/// fuera y alguien de la casa dice que llegó.
/// </summary>
public enum CryptoPaymentConfirmationStatus
{
    /// <summary>El alta se completó y está esperando a que alguien confirme que el pago entró.</summary>
    AwaitingPayment = 1,

    /// <summary>Alguien con permiso confirmó el cobro. A partir de aquí el miembro está activo.</summary>
    Confirmed = 2
}
