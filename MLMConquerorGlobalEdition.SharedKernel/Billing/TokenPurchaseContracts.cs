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
    public static bool IsBalance(string? method) =>
        string.Equals(method, CommissionBalance, StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, EWallet, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Valores de <see cref="TokenPurchaseResultDto.Status"/>.</summary>
public static class TokenPurchaseStatuses
{
    public const string Pending                    = "Pending";
    public const string Completed                  = "Completed";
    public const string Failed                     = "Failed";
    public const string AwaitingCryptoConfirmation = "AwaitingCryptoConfirmation";
    public const string NeedsReconciliation        = "NeedsReconciliation";
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
    public const string CryptoPaymentNotFound         = "CRYPTO_PAYMENT_NOT_FOUND";
    public const string CryptoAlreadyConfirmed        = "CRYPTO_PAYMENT_ALREADY_CONFIRMED";
    public const string CryptoTransactionIdRequired   = "CRYPTO_TRANSACTION_ID_REQUIRED";
    public const string PurchaseNotFound              = "TOKEN_PURCHASE_NOT_FOUND";
    public const string NotUnderReview                = "TOKEN_PURCHASE_NOT_UNDER_REVIEW";
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
