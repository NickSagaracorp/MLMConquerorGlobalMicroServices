using System.Net;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using static MLMConquerorGlobalEdition.SharedKernel.Billing.TokenPurchaseErrors;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Tokens;

/// <summary>
/// Traduce los códigos de compra y distribución a claves de recurso, y decide si la clave de
/// idempotencia sobrevive a un fallo. El miembro nunca ve el texto crudo de Billing ni de i-Payout.
/// </summary>
public static class TokenPurchaseMessages
{
    public static string ErrorKeyFor(string? code) => code switch
    {
        MonthlyLimitReached                                                => "Tokens.Error.MonthlyLimit",
        InsufficientCommissionBalance                                      => "Tokens.Error.InsufficientCommissions",
        InsufficientEWalletBalance                                         => "Tokens.Error.InsufficientEWallet",
        EWalletNotFound                                                    => "Tokens.Error.NoEWallet",
        EWalletRejected                                                    => "Tokens.Error.EWalletDeclined",
        NeedsReview                                                        => "Tokens.Error.NeedsReview",
        TokenTypeNotFound or NotForSale or NoGrantedProduct
            or ProductInactive or PriceNotSet                              => "Tokens.Error.NotPurchasable",
        CreditCardNotFound or CreditCardExpired or CreditCardNotChargeable => "Tokens.Error.Card",
        CardChargeFailed or NoImmediateGateway                             => "Tokens.Error.CardDeclined",
        CardPaymentsDisabled                                               => "Tokens.Error.CardDisabled",
        CryptoAddressNotConfigured or CryptoCurrencyNotSupported           => "Tokens.CryptoUnavailable",
        NotAwaitingCrypto                                                  => "Tokens.Error.NotAwaitingCrypto",
        InProgress or Busy                                                 => "Tokens.Error.InProgress",
        Impersonation or "IMPERSONATION_READ_ONLY"                         => "Tokens.Error.Impersonation",
        TokenDistributionErrors.Impersonation                              => "Tokens.Error.ImpersonationDistribute",
        MemberInactive                                                     => "Tokens.Error.MemberInactive",
        BillingUnavailable or EWalletUnavailable
            or PaymentServiceUnavailable                                   => "Tokens.Error.Unavailable",
        BillingContractError or BillingAuthMismatch                        => "Tokens.Error.ContactSupport",

        // El permiso ya se usó o la caché no respondió: se pide un código nuevo y se repite.
        StepUpGrant.RequiredErrorCode or StepUpGrant.InvalidErrorCode      => "StepUp.RetryFailed",

        "INSUFFICIENT_TOKEN_BALANCE"                                       => "Tokens.Error.NotEnoughTokens",
        "RECIPIENT_NOT_IN_DOWNLINE" or "RECIPIENT_NOT_FOUND"
            or "INVALID_RECIPIENT"                                         => "Tokens.Error.Recipient",
        TokenDistributionErrors.RecipientNotActive                         => "Tokens.Error.RecipientNotActive",

        // IDEMPOTENCY_KEY_REUSED, PAYMENT_METHOD_WRONG_ENDPOINT y lo desconocido.
        _                                                                  => "Tokens.Error.Generic"
    };

    /// <summary>
    /// ¿Hay que conservar la clave? Sí cuando no se sabe si la compra entró (caída, en curso, ocupado,
    /// una respuesta 5xx sin código) o cuando quedó en revisión: repetir con la misma clave devuelve lo
    /// ya hecho, y una clave nueva podría cobrar dos veces. También tras un rechazo del permiso de
    /// step-up: no se ejecutó nada y la repetición, con un código nuevo, debe ser la misma compra.
    /// </summary>
    /// <remarks>
    /// Un fallo definitivo cierra esa clave: <c>IDEMPOTENCY_KEY_REUSED</c>, <c>CARD_PAYMENTS_DISABLED</c>,
    /// <c>PAYMENT_SERVICE_UNAVAILABLE</c> (Billing abandonó la compra antes de cobrar) y los fallos de
    /// configuración entre servicios (<c>BILLING_CONTRACT_ERROR</c>, <c>BILLING_AUTH_MISMATCH</c>), que
    /// no se reintentan solos.
    /// </remarks>
    public static bool KeepsKey(string? code, HttpStatusCode status) =>
        code is InProgress or Busy or BillingUnavailable or NeedsReview
            or StepUpGrant.RequiredErrorCode or StepUpGrant.InvalidErrorCode or StepUpGrant.CooldownErrorCode
        || (string.IsNullOrEmpty(code) && (int)status >= 500);

    /// <summary>
    /// Texto de un fallo al distribuir. Como <see cref="ErrorKeyFor"/>, salvo lo que en distribuir
    /// significa otra cosa: la clave ya usada es "esa distribución ya se envió" (quizá se hizo), y
    /// ocupado o caché caída tienen su texto propio (los de compra hablan de pagos).
    /// </summary>
    public static string DistributionErrorKeyFor(string? code) => code switch
    {
        IdempotencyKeyReused                           => "Tokens.Error.DistributionRepeated",
        Busy                                           => "Tokens.Error.DistributionBusy",
        TokenDistributionErrors.IdempotencyUnavailable => "Tokens.Error.DistributionUnavailable",
        _                                              => ErrorKeyFor(code)
    };

    /// <summary>
    /// ¿Se conserva la clave de la distribución? Sí cuando no se movió nada y se puede repetir lo mismo
    /// (candado ocupado, caché caída al reclamar la clave) o cuando no se sabe si se hizo (5xx sin
    /// código; la caída de red la trata la pantalla): repetir con la misma clave no mueve los códigos
    /// dos veces. Tras un éxito o un fallo definitivo (destinatario, saldo, clave ya usada) se renueva.
    /// </summary>
    public static bool DistributionKeepsKey(string? code, HttpStatusCode status) =>
        code is Busy or TokenDistributionErrors.IdempotencyUnavailable
        || (string.IsNullOrEmpty(code) && (int)status >= 500);

    /// <summary>Texto de una compra en cripto que salió sin pagar. Null para los demás estados.</summary>
    public static string? StatusKeyFor(string? status) => status switch
    {
        TokenPurchaseStatuses.Cancelled => "Tokens.Status.Cancelled",
        TokenPurchaseStatuses.Rejected  => "Tokens.Status.Rejected",
        TokenPurchaseStatuses.Expired   => "Tokens.Status.Expired",
        _                               => null
    };
}
