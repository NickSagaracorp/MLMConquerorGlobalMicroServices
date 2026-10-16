namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

/// <summary>Códigos de error del alta con Entrax que ve la pantalla.</summary>
public static class EntraxSignupErrors
{
    /// <summary>Apagado o país no configurado. 400.</summary>
    public const string NotAvailable    = "ENTRAX_NOT_AVAILABLE";

    /// <summary>Entrax no emitió un QR usable. 502: el aspirante puede reintentar u otro método.</summary>
    public const string Unavailable     = "ENTRAX_UNAVAILABLE";

    /// <summary>Otra petición está creando el QR de este alta. 409.</summary>
    public const string InProgress      = "ENTRAX_IN_PROGRESS";

    /// <summary>
    /// Otra ventana tiene un QR vivo (o recién pedido) de este alta: no se emite otro. 409. La pantalla
    /// no reintenta; le dice a la persona que ya hay un QR activo en otra ventana. No se devuelve ese
    /// mismo QR porque la imagen no se guarda (solo el enlace) y no se puede reconstruir sin pedirla otra vez.
    /// </summary>
    public const string QrAlreadyActive = "ENTRAX_QR_ALREADY_ACTIVE";

    /// <summary>
    /// El QR de otra ventana ya caducó pero el servidor aún no lo cerró (hasta unos 12 min, mientras
    /// espera la confirmación del banco): no se puede empezar otro pago todavía. 409. La pantalla lo dice
    /// y no reintenta.
    /// </summary>
    public const string PaymentConfirming = "ENTRAX_PAYMENT_CONFIRMING";

    public const string NothingToPay    = "ENTRAX_NOTHING_TO_PAY";

    public const string PaymentNotFound = "ENTRAX_PAYMENT_NOT_FOUND";
}

public static class EntraxGateway
{
    /// <summary>PaymentHistory.GatewayName de los cobros de Entrax.</summary>
    public const string Name     = "Entrax";

    /// <summary>Moneda en la que se pide el QR del alta: la del pedido. Entrax convierte a rublos.</summary>
    public const string Currency = "USD";

    /// <summary>paymentId de Entrax normalizado (GUID "D" en minúsculas) o null si no es un GUID.</summary>
    public static string? NormalizePaymentId(string? id)
        => Guid.TryParse(id, out var g) ? g.ToString("D") : null;

    public static int SecondsUntil(DateTime? expiresAt, DateTime now)
        => expiresAt is null ? 0 : (int)Math.Max(0, Math.Ceiling((expiresAt.Value - now).TotalSeconds));
}
