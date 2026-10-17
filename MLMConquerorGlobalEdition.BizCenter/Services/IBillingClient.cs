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

    /// <summary>"Pagar ahora" la cuota anual (Plan K). El permiso de step-up ya lo validó y quemó el filtro.</summary>
    Task<BillingResult<AnnualFeePaymentResultDto>> PayAnnualFeeAsync(
        PayAnnualFeeRequest request, string bearerToken, string? stepUpGrant, CancellationToken ct = default);
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

    /// <summary>
    /// Billing contestó un 4xx sin código: no ejecutó nada, pero no hay acuerdo de contrato entre los dos
    /// servicios. 502 porque el fallo es de la pasarela, no del miembro; la pantalla NO conserva la clave
    /// ni reintenta sola.
    /// </summary>
    public static BillingResult<T> ContractError() =>
        Fail(StatusCodes.Status502BadGateway, TokenPurchaseErrors.BillingContractError,
             "The payment service could not process this request. Please contact support.");

    /// <summary>
    /// Billing rechazó con 401 el token que el BizCenter aceptó. Nunca se devuelve 401: sacaría al
    /// miembro de una sesión que es válida. La pantalla NO conserva la clave ni reintenta sola.
    /// </summary>
    public static BillingResult<T> AuthMismatch() =>
        Fail(StatusCodes.Status502BadGateway, TokenPurchaseErrors.BillingAuthMismatch,
             "The payment service is misconfigured right now. Please contact support.");
}
