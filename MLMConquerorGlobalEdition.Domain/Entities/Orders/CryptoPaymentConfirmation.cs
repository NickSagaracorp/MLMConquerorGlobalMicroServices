using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Un alta pagada en cripto que espera confirmación manual, y el rastro de quién la confirmó.
///
/// POR QUÉ ES UNA TABLA Y NO UNA COLUMNA EN Orders: aprobar aquí mueve dinero. Activa una
/// membresía y dispara comisiones al upline. Lo que hay que poder reconstruir después es quién
/// dijo que el dinero había llegado, cuándo lo dijo y contra qué identificador de transacción,
/// y eso son cuatro campos que solo existen en esta vía de pago. Colgarlos de Orders los deja
/// nulos en el 100% de las altas por tarjeta y por token.
///
/// POR QUÉ NO EN AuthSecurityEvents: esa bitácora es de AUTENTICACIÓN —2FA, enrolamiento, alta
/// de teléfono, cambios de contraseña, step-up—. Su clave ajena es a AspNetUsers, sus columnas
/// son Channel, ChallengeJti y OperationKey, y su AuthEventType no tiene ni puede tener un valor
/// para "cobro en cripto confirmado" sin desvirtuar lo que la tabla significa. Un cobro no es un
/// evento de sesión. Aquí el sujeto del rastro no es el usuario que se autentica sino el pedido
/// que se cobra, y el actor es un tercero (el administrador). Meterlo allí obligaría a elegir
/// entre poner en UserId al miembro (y perder quién aprobó) o al administrador (y perder de
/// quién es el cobro).
///
/// La unicidad de OrderId es lo que impide dos confirmaciones para el mismo pedido, y RowVersion
/// es lo que impide que dos personas confirmen la misma fila a la vez.
/// </summary>
public class CryptoPaymentConfirmation : AuditChangesStringKey
{
    /// <summary>El pedido del alta. Único: un pedido no se cobra dos veces.</summary>
    public string OrderId { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;

    /// <summary>Desnormalizado a propósito: el rastro sobrevive a la baja del perfil.</summary>
    public string MemberEmail { get; set; } = string.Empty;

    /// <summary>Lo que el aspirante eligió pagar: BTC, ETH, USDT…</summary>
    public string CryptoCurrency { get; set; } = string.Empty;

    /// <summary>Importe del pedido en el momento del alta. Congelado aquí para el cotejo manual.</summary>
    public decimal AmountDue { get; set; }

    public CryptoPaymentConfirmationStatus Status { get; set; } = CryptoPaymentConfirmationStatus.AwaitingPayment;

    /// <summary>El hash de la transferencia. Se captura AL CONFIRMAR, no al completar el alta.</summary>
    public string? CryptoTransactionId { get; set; }

    /// <summary>Id del usuario administrador que confirmó. Sale del JWT, no del cuerpo de la petición.</summary>
    public string? ConfirmedByUserId { get; set; }

    /// <summary>Correo del administrador que confirmó, desnormalizado por la misma razón que MemberEmail.</summary>
    public string? ConfirmedByEmail { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    /// <summary>Nota opcional del administrador: red usada, importe recibido, incidencias.</summary>
    public string? Notes { get; set; }
}
