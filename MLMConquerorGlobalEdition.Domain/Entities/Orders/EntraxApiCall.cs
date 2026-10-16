using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Una llamada a Entrax o un callback de Entrax. SOLO campos estructurados: ni cuerpos ni cabeceras.
/// Las respuestas de Entrax pueden traer el teléfono del pagador (transactionDetails.payerPhone) y
/// el legado llegó a registrar la contraseña del formulario; aquí no hay nada personal que filtrar.
/// </summary>
public class EntraxApiCall : AuditChangesLongKey
{
    /// <summary>CreateQr, GetPayment o Callback.</summary>
    public string Operation { get; set; } = string.Empty;

    public string? ExternalOrderId   { get; set; }
    public string? ExternalPaymentId { get; set; }

    /// <summary>0 si no hubo respuesta HTTP (timeout, red) o si es un callback entrante.</summary>
    public int HttpStatusCode { get; set; }

    public bool Success { get; set; }

    /// <summary>Código de error de Entrax (RATE_NOT_AVAILABLE…) o nuestro (TRANSPORT, BAD_RESPONSE).</summary>
    public string? ErrorCode { get; set; }

    /// <summary>pending, paid, failed, expired, SUCCESS, FAILED… tal como llegó.</summary>
    public string? ProviderStatus { get; set; }

    public int DurationMs { get; set; }
}
