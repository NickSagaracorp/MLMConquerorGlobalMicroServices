using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Un intento de pagar un alta en rublos por SBP con Entrax. Un pedido puede tener varios (un QR
/// caduca y se pide otro), pero un pago de Entrax activa como mucho un alta: ExternalPaymentId es
/// único y Consumed solo se pone una vez, bajo el RowVersion de la fila.
///
/// El importe que manda es el del SERVIDOR (Orders.TotalAmount, en USD) congelado en AmountUsd al
/// pedir el QR; Entrax convierte a rublos. Nunca se convierte aquí.
/// </summary>
public class EntraxPayment : AuditChangesStringKey
{
    public string OrderId  { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;

    /// <summary>"SIGNUP-{OrderId}-{intento}". Único. Es el orderId que ve Entrax.</summary>
    public string ExternalOrderId { get; set; } = string.Empty;

    /// <summary>paymentId de Entrax (GUID en minúsculas). Null hasta que Entrax responde. Único.</summary>
    public string? ExternalPaymentId { get; set; }

    public EntraxPaymentStatus Status { get; set; } = EntraxPaymentStatus.AwaitingPayment;

    /// <summary>Importe pedido en USD: el total del pedido al emitir el QR, redondeado a 2 decimales.</summary>
    public decimal AmountUsd { get; set; }

    /// <summary>Lo que Entrax dijo que se pagaría en rublos y con qué cambio. Informativo.</summary>
    public decimal? AmountRub    { get; set; }
    public decimal? ExchangeRate { get; set; }

    /// <summary>amount.original que devolvió GET /payment al liquidarlo como pagado.</summary>
    public decimal? PaidAmountUsd { get; set; }

    /// <summary>Enlace https del QR. La imagen no se guarda (es grande y se puede pedir otra).</summary>
    public string? QrLink { get; set; }

    /// <summary>Caducidad del QR en hora de servidor.</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? PaidAt { get; set; }

    /// <summary>Este pago ya activó un alta. Nunca vuelve a false.</summary>
    public bool Consumed { get; set; }

    public DateTime? ActivatedAt { get; set; }

    /// <summary>Última vez que se consultó GET /payment. Estrangula la consulta de la pantalla.</summary>
    public DateTime? LastCheckedAt { get; set; }

    /// <summary>Código de por qué falló o quedó a revisar (p. ej. AMOUNT_TOO_LOW).</summary>
    public string? FailureCode { get; set; }

    public string? Notes { get; set; }

    public static string BuildExternalOrderId(string orderId, int attempt) => $"SIGNUP-{orderId}-{attempt}";
}
