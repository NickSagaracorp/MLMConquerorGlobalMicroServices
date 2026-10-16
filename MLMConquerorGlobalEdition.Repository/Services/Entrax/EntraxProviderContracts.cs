namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>Lo que se pide a POST /qr. Amount en la moneda indicada (en el alta, USD).</summary>
public sealed record EntraxQrRequest(
    decimal Amount, string Currency, string OrderId, string? Description, string? CallbackUrl);

/// <summary>Lo que devolvió POST /qr, ya fuera del sobre.</summary>
public sealed record EntraxQr(
    string PaymentId,
    string? ImageDataUri,
    string? Link,
    decimal AmountInRubles,
    decimal ExchangeRate,
    string Currency,
    decimal OriginalAmount,
    DateTimeOffset? ExpiresAt,
    string? OrderId);

/// <summary>
/// Lo que devolvió GET /payment/{id}. Status: pending | paid | failed | expired. No lleva
/// transactionDetails (teléfono del pagador): no se necesita y así no puede acabar en ningún sitio.
/// </summary>
public sealed record EntraxPaymentState(
    string PaymentId,
    string Status,
    string? OrderId,
    decimal Original,
    string Currency,
    decimal Rubles,
    decimal ExchangeRate,
    DateTimeOffset? CompletedAt);

/// <summary>Resultado de una llamada. El cliente nunca lanza por un fallo del proveedor o de la red.</summary>
public sealed record EntraxCall<T>(T? Value, int HttpStatus, string? ErrorCode, string? ErrorMessage, long ElapsedMs)
    where T : class
{
    public bool Ok => Value is not null && ErrorCode is null;

    public static EntraxCall<T> Success(T value, int httpStatus, long elapsedMs)
        => new(value, httpStatus, null, null, elapsedMs);

    public static EntraxCall<T> Fail(int httpStatus, string errorCode, string? message, long elapsedMs)
        => new(null, httpStatus, errorCode, message, elapsedMs);
}

public static class EntraxErrorCodes
{
    /// <summary>Sin respuesta: timeout, DNS, conexión rechazada.</summary>
    public const string Transport        = "TRANSPORT";

    /// <summary>Respuesta que no es el sobre esperado o le faltan campos.</summary>
    public const string BadResponse      = "BAD_RESPONSE";

    /// <summary>paymentId que no es un GUID: no se llama (evita rutas inyectadas).</summary>
    public const string InvalidPaymentId = "INVALID_PAYMENT_ID";
}

public interface IEntraxClient
{
    Task<EntraxCall<EntraxQr>> CreateQrAsync(EntraxQrRequest request, CancellationToken ct);
    Task<EntraxCall<EntraxPaymentState>> GetPaymentAsync(string paymentId, CancellationToken ct);
}
