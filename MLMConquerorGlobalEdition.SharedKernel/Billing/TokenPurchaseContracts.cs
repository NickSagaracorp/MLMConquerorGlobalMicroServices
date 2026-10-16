namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Reglas de la compra de tokens. Las aplica Billing, las reenvía el BizCenter y las muestra la
/// pantalla; viven aquí para que las tres partes no diverjan (la nota de la pantalla decía "10 por
/// mes" mientras el negocio pedía 50).
/// </summary>
public static class TokenPurchaseRules
{
    /// <summary>Tokens que un miembro puede comprar por mes calendario, en hora de servidor.</summary>
    public const int MonthlyLimit = 50;

    public const int IdempotencyKeyMaxLength = 64;

    /// <summary>
    /// Monedas candidatas. Solo se ofrecen las que tienen dirección de depósito configurada
    /// (<see cref="CryptoDepositKeys"/>).
    /// </summary>
    public static readonly IReadOnlyList<string> CryptoCurrencies = ["BTC", "ETH", "USDT"];

    /// <summary>La clave la genera la pantalla (un GUID sin guiones).</summary>
    public static bool IsValidIdempotencyKey(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Length <= IdempotencyKeyMaxLength
        && key.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>yyyyMM del mes al que se imputa una reserva.</summary>
    public static int PeriodKey(DateTime now) => now.Year * 100 + now.Month;

    public static DateTime MonthStart(DateTime now) => new(now.Year, now.Month, 1, 0, 0, 0, now.Kind);

    /// <summary>El primer día del mes de un <see cref="PeriodKey"/> (yyyyMM), para medir el cupo de ese mes.</summary>
    public static DateTime MonthOf(int periodKey) => new(periodKey / 100, periodKey % 100, 1);

    /// <summary>
    /// Clave de configuración de Billing con los días que una compra en cripto puede esperar la
    /// transferencia antes de caducar y soltar el cupo.
    /// </summary>
    public const string CryptoExpiryDaysConfigKey = "Billing:TokenPurchase:CryptoExpiryDays";

    public const int DefaultCryptoExpiryDays = 3;

    /// <summary>
    /// ¿Se acepta tarjeta? Apagado por defecto: el gateway de Spreedly es todavía un stub que devuelve
    /// un éxito simulado, y encenderlo emitiría tokens sin cobrar. Se enciende solo cuando el cobro con
    /// tarjeta sea real (bloqueante de producción, ver el plan F).
    /// </summary>
    public const string CardEnabledConfigKey = "Billing:TokenPurchase:CardEnabled";

    /// <summary>
    /// Minutos que una compra con tarjeta o eWallet puede seguir en Pending antes de que el barrido la
    /// pase a conciliar (el proceso murió entre el cobro y la emisión).
    /// </summary>
    public const string StalePendingMinutesConfigKey = "Billing:TokenPurchase:StalePendingMinutes";

    public const int DefaultStalePendingMinutes = 30;
}

/// <summary>Valores de <see cref="PurchaseTokensRequest.PaymentMethod"/>. Coinciden con el enum de dominio.</summary>
public static class TokenPaymentMethods
{
    public const string CommissionBalance = "CommissionBalance";
    public const string EWallet           = "EWallet";
    public const string CreditCard        = "CreditCard";
    public const string Crypto            = "Crypto";

    /// <summary>
    /// Pagar con saldo propio (comisiones o eWallet) exige step-up: es dinero que ya es del miembro y
    /// sale sin que nadie más lo autorice. Tarjeta (emisor) y cripto (admin) tienen su propio control.
    /// </summary>
    /// <remarks>
    /// Recorta los espacios igual que el servicio de Billing al leer el método: si no, " EWallet"
    /// no contaría como saldo aquí (sin step-up) y sí como eWallet al cobrar.
    /// </remarks>
    public static bool IsBalance(string? method) =>
        string.Equals(method?.Trim(), CommissionBalance, StringComparison.OrdinalIgnoreCase)
        || string.Equals(method?.Trim(), EWallet, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Valores de <see cref="TokenPurchaseResultDto.Status"/>.</summary>
public static class TokenPurchaseStatuses
{
    public const string Pending                    = "Pending";
    public const string Completed                  = "Completed";
    public const string Failed                     = "Failed";
    public const string AwaitingCryptoConfirmation = "AwaitingCryptoConfirmation";
    public const string NeedsReconciliation        = "NeedsReconciliation";

    // Salidas de una compra en cripto sin pagar: ninguna emite y las tres liberan el cupo.
    public const string Cancelled                  = "Cancelled";
    public const string Rejected                   = "Rejected";
    public const string Expired                    = "Expired";
}

/// <summary>Códigos de error de la compra. La pantalla los traduce con <c>TokenPurchaseMessages</c>.</summary>
public static class TokenPurchaseErrors
{
    public const string NotAMember                    = "NOT_A_MEMBER";
    public const string Impersonation                 = "IMPERSONATION_CANNOT_PURCHASE";
    public const string InvalidQuantity               = "INVALID_QUANTITY";
    public const string IdempotencyKeyRequired        = "IDEMPOTENCY_KEY_REQUIRED";
    public const string PaymentMethodNotSupported     = "PAYMENT_METHOD_NOT_SUPPORTED";

    /// <summary>Un método de saldo por la ruta sin step-up, o tarjeta/cripto por la de saldo.</summary>
    public const string WrongEndpoint                 = "PAYMENT_METHOD_WRONG_ENDPOINT";
    public const string MemberNotFound                = "MEMBER_NOT_FOUND";
    public const string MemberInactive                = "MEMBER_INACTIVE";
    public const string TokenTypeNotFound             = "TOKEN_TYPE_NOT_FOUND";
    public const string NotForSale                    = "TOKEN_TYPE_NOT_FOR_SALE";
    public const string NoGrantedProduct              = "TOKEN_TYPE_NO_PRODUCT";
    public const string ProductInactive               = "TOKEN_PRODUCT_INACTIVE";
    public const string PriceNotSet                   = "TOKEN_PRICE_NOT_SET";
    public const string MonthlyLimitReached           = "TOKEN_MONTHLY_LIMIT_REACHED";
    public const string InsufficientCommissionBalance = "INSUFFICIENT_COMMISSION_BALANCE";
    public const string InsufficientEWalletBalance    = "INSUFFICIENT_EWALLET_BALANCE";
    public const string EWalletNotFound               = "EWALLET_NOT_FOUND";
    public const string EWalletUnavailable            = "EWALLET_UNAVAILABLE";
    public const string EWalletRejected               = "EWALLET_DEBIT_REJECTED";
    public const string NeedsReview                   = "PURCHASE_NEEDS_REVIEW";
    public const string CreditCardNotFound            = "CREDIT_CARD_NOT_FOUND";
    public const string CreditCardExpired             = "CREDIT_CARD_EXPIRED";
    public const string CreditCardNotChargeable       = "CREDIT_CARD_NOT_CHARGEABLE";
    public const string CardChargeFailed              = "CARD_CHARGE_FAILED";
    public const string NoImmediateGateway            = "NO_IMMEDIATE_GATEWAY";
    public const string CryptoCurrencyNotSupported    = "CRYPTO_CURRENCY_NOT_SUPPORTED";
    public const string CryptoAddressNotConfigured    = "CRYPTO_ADDRESS_NOT_CONFIGURED";
    public const string InProgress                    = "PURCHASE_IN_PROGRESS";
    public const string Busy                          = "PURCHASE_BUSY";
    public const string BillingUnavailable            = "BILLING_UNAVAILABLE";

    /// <summary>
    /// Billing contestó un 4xx sin código (ProblemDetails, 404 de ruta, 405, 415): no ejecutó nada, pero
    /// el BizCenter y Billing no se entienden. No se reintenta con la misma clave: es un fallo nuestro.
    /// </summary>
    public const string BillingContractError          = "BILLING_CONTRACT_ERROR";

    /// <summary>
    /// Billing rechazó con 401 el token que el BizCenter ya había aceptado: desacuerdo de configuración
    /// entre servicios (llave, emisor, audiencia), no una sesión caducada. No se reintenta solo.
    /// </summary>
    public const string BillingAuthMismatch           = "BILLING_AUTH_MISMATCH";
    public const string CryptoPaymentNotFound         = "CRYPTO_PAYMENT_NOT_FOUND";
    public const string CryptoAlreadyConfirmed        = "CRYPTO_PAYMENT_ALREADY_CONFIRMED";
    public const string CryptoTransactionIdRequired   = "CRYPTO_TRANSACTION_ID_REQUIRED";
    public const string PurchaseNotFound              = "TOKEN_PURCHASE_NOT_FOUND";
    public const string NotUnderReview                = "TOKEN_PURCHASE_NOT_UNDER_REVIEW";

    /// <summary>Cancelar o rechazar algo que no es una compra en cripto pendiente de confirmar.</summary>
    public const string NotAwaitingCrypto             = "TOKEN_PURCHASE_NOT_AWAITING_CRYPTO";

    /// <summary>
    /// "Reabrir y confirmar" solo vale para una compra en cripto CADUCADA. Una cancelada por el miembro o
    /// rechazada por un administrador no se reabre: es una decisión de alguien, no un plazo vencido.
    /// </summary>
    public const string NotExpired                    = "TOKEN_PURCHASE_NOT_EXPIRED";

    /// <summary>El pago con tarjeta está apagado (Billing:TokenPurchase:CardEnabled = false).</summary>
    public const string CardPaymentsDisabled          = "CARD_PAYMENTS_DISABLED";

    /// <summary>
    /// Motivo de conciliación (no se devuelve al miembro): el gateway contestó con un id de transacción
    /// simulado. No hubo cobro real y no se emite.
    /// </summary>
    public const string CardSimulatedCharge           = "CARD_SIMULATED_CHARGE";

    /// <summary>La misma clave de idempotencia con otro tipo, cantidad o método: no es un reintento.</summary>
    public const string IdempotencyKeyReused          = "IDEMPOTENCY_KEY_REUSED";

    /// <summary>
    /// Billing no pudo empezar el cobro (base de datos, candado o ruteo fallaron antes de mover dinero).
    /// La compra queda fallida y se puede volver a intentar con otra clave.
    /// </summary>
    public const string PaymentServiceUnavailable     = "PAYMENT_SERVICE_UNAVAILABLE";

    /// <summary>
    /// Confirmar o conciliar no pudo emitir (colisión de códigos dos veces seguidas). No se guardó nada;
    /// se puede repetir.
    /// </summary>
    public const string IssueFailed                   = "TOKEN_ISSUE_FAILED";
}

/// <summary>
/// Códigos de distribuir o transferir tokens en el BizCenter. El candado ocupado es
/// <see cref="TokenPurchaseErrors.Busy"/>. Los usan el servidor (handlers, controlador) y la pantalla.
/// </summary>
public static class TokenDistributionErrors
{
    /// <summary>Quien envía y quien recibe son el mismo miembro (400).</summary>
    public const string InvalidRecipient = "INVALID_RECIPIENT";

    /// <summary>El destinatario no existe (400).</summary>
    public const string RecipientNotFound = "RECIPIENT_NOT_FOUND";

    /// <summary>El destinatario no está en el equipo de inscripción de quien envía (400).</summary>
    public const string RecipientNotInDownline = "RECIPIENT_NOT_IN_DOWNLINE";

    /// <summary>Quien envía no tiene bastantes códigos disponibles de ese tipo (400).</summary>
    public const string InsufficientTokenBalance = "INSUFFICIENT_TOKEN_BALANCE";

    /// <summary>Distribuir o transferir mueve tokens a otra persona: nunca suplantando (403).</summary>
    public const string Impersonation = "IMPERSONATION_CANNOT_DISTRIBUTE";

    /// <summary>
    /// El destinatario existe y es del equipo, pero su cuenta no está activa (inactiva, suspendida,
    /// terminada o pendiente de activar): no se le dan tokens que no podría usar (400).
    /// </summary>
    public const string RecipientNotActive = "RECIPIENT_NOT_ACTIVE";

    /// <summary>
    /// No se pudo reclamar la clave de idempotencia de la distribución (la caché no respondió). Falla
    /// cerrado: no se distribuyó nada y se puede repetir con la misma clave (503).
    /// </summary>
    public const string IdempotencyUnavailable = "IDEMPOTENCY_UNAVAILABLE";

    /// <summary>Cabecera con la clave de idempotencia de la distribución (un GUID sin guiones por intento).</summary>
    public const string IdempotencyHeader = "Idempotency-Key";

    /// <summary>Cuánto dura reclamada una clave de distribución.</summary>
    public static readonly TimeSpan IdempotencyKeyTtl = TimeSpan.FromHours(24);

    /// <summary>Clave en la caché de la reclamación de una distribución.</summary>
    public static string IdempotencyCacheKey(string memberId, string key) => $"dist:{memberId}:{key}";
}

/// <summary>
/// Lo que manda la pantalla. NO lleva MemberId: el comprador sale del token en el BizCenter y otra
/// vez en Billing. Un campo que no existe no se puede falsificar.
/// </summary>
public sealed class PurchaseTokensRequest
{
    public int     TokenTypeId    { get; set; }
    public int     Quantity       { get; set; }
    public string  PaymentMethod  { get; set; } = string.Empty;
    public string? CreditCardId   { get; set; }
    public string? CryptoCurrency { get; set; }
    public string  IdempotencyKey { get; set; } = string.Empty;
}

public sealed class TokenPurchaseResultDto
{
    public string       PurchaseId           { get; set; } = string.Empty;
    public string       OrderId              { get; set; } = string.Empty;
    public string?      OrderNo              { get; set; }
    public string       Status               { get; set; } = string.Empty;
    public int          TokenTypeId          { get; set; }
    public int          Quantity             { get; set; }
    public decimal      UnitPrice            { get; set; }
    public decimal      TotalAmount          { get; set; }
    public string       PaymentMethod        { get; set; } = string.Empty;
    public List<string> TokenCodes           { get; set; } = new();
    public int          MonthlyRemaining     { get; set; }

    // Solo en cripto: a dónde y en qué moneda transferir.
    public string?      CryptoCurrency       { get; set; }
    public string?      CryptoDepositAddress { get; set; }
    public string?      CryptoNetwork        { get; set; }
}

public sealed class TokenCatalogDto
{
    public int MonthlyLimit       { get; set; }
    public int PurchasedThisMonth { get; set; }
    public int MonthlyRemaining   { get; set; }
    public List<TokenCatalogItemDto> Items { get; set; } = new();

    /// <summary>Monedas con dirección configurada. Vacío = no se ofrece cripto.</summary>
    public List<CryptoDepositAddressDto> CryptoDeposits { get; set; } = new();

    /// <summary>
    /// False si el pago con tarjeta está apagado en Billing: la pantalla no muestra la pestaña. Billing
    /// lo rechaza igualmente (CARD_PAYMENTS_DISABLED) si alguien lo pide.
    /// </summary>
    public bool CardEnabled { get; set; }
}

public sealed class TokenCatalogItemDto
{
    public int      TokenTypeId          { get; set; }
    public string   Name                 { get; set; } = string.Empty;
    public string?  Description          { get; set; }
    public bool     IsGuestPass          { get; set; }
    public decimal? UnitPrice            { get; set; }
    public bool     Purchasable          { get; set; }
    public string?  NotPurchasableReason { get; set; }
}

public sealed class TokenPurchaseFundsDto
{
    public decimal  CommissionBalance { get; set; }

    /// <summary>Saldo real en i-Payout (USD). Null si no se pudo leer o no hay cuenta.</summary>
    public decimal? EWalletBalance    { get; set; }

    /// <summary>False si el miembro no tiene cuenta eWallet registrada: no puede pagar con ella.</summary>
    public bool     HasEWallet        { get; set; }

    /// <summary>Igual que <see cref="TokenCatalogDto.CardEnabled"/>, para quien solo pide los fondos.</summary>
    public bool     CardEnabled       { get; set; }
}

public sealed class TokenSummaryDto
{
    public int TotalTokens    { get; set; }
    public int TotalUsed      { get; set; }
    public int TotalAvailable { get; set; }
}

public sealed class ConfirmCryptoTokenPurchaseRequest
{
    public string  CryptoTransactionId { get; set; } = string.Empty;
    public string? Notes               { get; set; }
}

/// <summary>
/// Cierre manual de una compra que quedó a conciliar (débito eWallet dudoso). Lo decide tesorería
/// después de mirar i-Payout.
/// </summary>
public sealed class ReconcileTokenPurchaseRequest
{
    /// <summary>True: el dinero salió; se emiten los tokens. False: no salió; se libera el cupo.</summary>
    public bool    Charged          { get; set; }
    public string? GatewayReference { get; set; }
    public string? Notes            { get; set; }
}

/// <summary>Un administrador rechaza una compra en cripto cuya transferencia no llegó o no vale.</summary>
public sealed class RejectCryptoTokenPurchaseRequest
{
    /// <summary>Por qué se rechaza; queda en las notas de la compra y de la confirmación.</summary>
    public string? Reason { get; set; }
}
