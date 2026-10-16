namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Estados del pago del alta con Entrax tal como los ve la pantalla. Son los nombres de
/// <c>Domain.Entities.Orders.EntraxPaymentStatus</c>; una prueba vigila que no se separen.
/// </summary>
public static class EntraxSignupStatuses
{
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Paid            = "Paid";
    public const string Failed          = "Failed";
    public const string Expired         = "Expired";
    public const string ToReview        = "ToReview";

    public static readonly IReadOnlyList<string> All = [AwaitingPayment, Paid, Failed, Expired, ToReview];
}

/// <summary>
/// Lo que devuelve completar un alta con Entrax: el QR que hay que pagar. Lo produce SignupAPI y lo
/// pinta <c>SharedComponents.Lite/Payments/EntraxQrPayment.razor</c>.
/// </summary>
public sealed class EntraxSignupPaymentDto
{
    /// <summary>
    /// Id de nuestro registro del pago (GUID aleatorio). Junto con el id del alta es lo que pide el
    /// endpoint de estado: sin los dos no se consulta nada.
    /// </summary>
    public string PaymentRef { get; set; } = string.Empty;

    /// <summary><c>data:image/png;base64,…</c> validado en el servidor, o null si Entrax mandó otra cosa.</summary>
    public string? QrImage { get; set; }

    /// <summary>Enlace https para abrir el pago en el móvil, o null si no era https.</summary>
    public string? QrLink { get; set; }

    public decimal AmountUsd    { get; set; }
    public decimal AmountRub    { get; set; }
    public decimal ExchangeRate { get; set; }

    /// <summary>Hora de servidor. Informativa: la cuenta atrás usa <see cref="SecondsRemaining"/>.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Segundos hasta que caduca el QR, calculados en el servidor.</summary>
    public int SecondsRemaining { get; set; }
}

/// <summary>Estado del pago para la pantalla del alta. No lleva nada más a propósito: el endpoint es anónimo.</summary>
public sealed class EntraxSignupStatusDto
{
    public string    Status           { get; set; } = EntraxSignupStatuses.AwaitingPayment;
    public DateTime? ExpiresAt        { get; set; }
    public int       SecondsRemaining { get; set; }
}

/// <summary>Métodos de pago del alta que dependen de la configuración del servidor.</summary>
public sealed class SignupPaymentOptionsDto
{
    /// <summary>Países (ISO2) en los que se ofrece pagar en rublos por SBP. Vacío = apagado.</summary>
    public IReadOnlyList<string> EntraxCountries { get; set; } = [];
}
