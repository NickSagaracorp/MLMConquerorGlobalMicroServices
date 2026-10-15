using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <summary>
/// Paso hacia Billing para la compra de tokens. El BizCenter no calcula precios, cupos ni saldos: se
/// reenvía el JWT del miembro y Billing saca de él quién compra.
/// </summary>
public interface IBillingClient
{
    Task<BillingResult<TokenCatalogDto>> GetTokenCatalogAsync(string bearerToken, CancellationToken ct = default);
    Task<BillingResult<TokenPurchaseFundsDto>> GetPurchaseFundsAsync(string bearerToken, CancellationToken ct = default);

    /// <param name="stepUpGrant">Permiso de step-up ya validado (y quemado) por el filtro; obligatorio para pagar con saldo.</param>
    Task<BillingResult<TokenPurchaseResultDto>> PurchaseTokensAsync(
        PurchaseTokensRequest request, string bearerToken, string? stepUpGrant = null, CancellationToken ct = default);

    /// <summary>Retira una compra en cripto sin pagar del miembro del token (D18).</summary>
    Task<BillingResult<TokenPurchaseResultDto>> CancelTokenPurchaseAsync(
        string purchaseId, string bearerToken, CancellationToken ct = default);
}

/// <summary>
/// Lo que contestó Billing: el dato, o el estado HTTP y el código de error que la pantalla sabe leer.
/// El estado viaja para devolverlo tal cual (un 409 IDEMPOTENCY_KEY_REUSED no es un 400).
/// </summary>
public sealed class BillingResult<T> where T : class
{
    public int     StatusCode { get; private init; }
    public T?      Value      { get; private init; }
    public string? ErrorCode  { get; private init; }
    public string? Error      { get; private init; }

    public bool IsSuccess => ErrorCode is null && Value is not null;

    public static BillingResult<T> Ok(T value) =>
        new() { StatusCode = StatusCodes.Status200OK, Value = value };

    public static BillingResult<T> Fail(int statusCode, string errorCode, string error) =>
        new() { StatusCode = statusCode, ErrorCode = errorCode, Error = error };

    /// <summary>
    /// Billing no contestó o contestó algo que no se puede leer: no se sabe si llegó a ejecutar, así
    /// que la pantalla conserva la clave de idempotencia y el reintento devuelve lo que ya se hizo.
    /// </summary>
    public static BillingResult<T> Unavailable(string error) =>
        Fail(StatusCodes.Status502BadGateway, TokenPurchaseErrors.BillingUnavailable, error);
}
