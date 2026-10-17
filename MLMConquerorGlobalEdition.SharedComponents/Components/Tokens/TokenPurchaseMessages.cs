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
    /// <summary>
    /// Lo que devuelve el BizCenter cuando algo lanza sin código propio (DomainExceptionMiddleware,
    /// ErrorHandlingBehavior): un 500 del que no se sabe si la compra llegó a hacerse.
    /// </summary>
    public const string InternalErrorCode = "INTERNAL_ERROR";

    /// <summary>
    /// Fallos 5xx que SÍ son definitivos: Billing abandonó la compra antes de cobrar
    /// (<c>PAYMENT_SERVICE_UNAVAILABLE</c>) o el BizCenter y Billing no se entienden
    /// (<c>BILLING_CONTRACT_ERROR</c>, <c>BILLING_AUTH_MISMATCH</c>). Cualquier otro 5xx conserva la clave.
    /// </summary>
    // Los 5xx definitivos son los de BillingRetry.DefinitiveServerErrors (comunes con la cuota anual).

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
        Impersonation or ImpersonationScope.ReadOnlyErrorCode              => "Tokens.Error.Impersonation",
        TokenDistributionErrors.Impersonation                              => "Tokens.Error.ImpersonationDistribute",
        MemberInactive                                                     => "Tokens.Error.MemberInactive",
        BillingUnavailable or EWalletUnavailable
            or PaymentServiceUnavailable or InternalErrorCode              => "Tokens.Error.Unavailable",
        BillingContractError or BillingAuthMismatch                        => "Tokens.Error.ContactSupport",

        // El permiso ya se usó o la caché no respondió: se pide un código nuevo y se repite.
        StepUpGrant.RequiredErrorCode or StepUpGrant.InvalidErrorCode      => "StepUp.RetryFailed",

        TokenDistributionErrors.InsufficientTokenBalance                   => "Tokens.Error.NotEnoughTokens",
        TokenDistributionErrors.RecipientNotInDownline
            or TokenDistributionErrors.RecipientNotFound                   => "Tokens.Error.Recipient",
        TokenDistributionErrors.InvalidRecipient                           => "Tokens.Error.SelfRecipient",
        TokenDistributionErrors.RecipientNotActive                         => "Tokens.Error.RecipientNotActive",

        // IDEMPOTENCY_KEY_REUSED, PAYMENT_METHOD_WRONG_ENDPOINT y lo desconocido.
        _                                                                  => "Tokens.Error.Generic"
    };

    /// <summary>
    /// ¿Hay que conservar la clave? Sí cuando no se sabe si la compra entró (caída, en curso, ocupado,
    /// cualquier 5xx que no esté en la lista de definitivos, con o sin código: <c>INTERNAL_ERROR</c>
    /// incluido) o cuando quedó en revisión: repetir con la misma clave devuelve lo ya hecho, y una clave
    /// nueva podría cobrar dos veces. También tras un rechazo del permiso de step-up: no se ejecutó nada
    /// y la repetición, con un código nuevo, debe ser la misma compra.
    /// </summary>
    /// <remarks>
    /// Ante la duda, se conserva: un 5xx solo cierra la clave si su código está en
    /// <see cref="Billing.BillingRetry.DefinitiveServerErrors"/>. Los 4xx con código son definitivos salvo los de arriba
    /// (<c>IDEMPOTENCY_KEY_REUSED</c>, <c>CARD_PAYMENTS_DISABLED</c>, el tope…). <c>STEP_UP_COOLDOWN</c>
    /// no está: el diálogo de step-up ya lo explica y no hay nada que repetir hasta que pase la espera.
    /// </remarks>
    public static bool KeepsKey(string? code, HttpStatusCode status) =>
        code is InProgress or Busy or BillingUnavailable or NeedsReview
        || Billing.BillingRetry.UnknownOrNotExecuted(code, status);

    /// <summary>
    /// Texto de un fallo al distribuir. Como <see cref="ErrorKeyFor"/>, salvo lo que en distribuir
    /// significa otra cosa: la clave ya usada es "esa distribución ya se envió" (quizá se hizo), y
    /// ocupado o caché caída tienen su texto propio (los de compra hablan de pagos).
    /// </summary>
    public static string DistributionErrorKeyFor(string? code) => code switch
    {
        // La clave ya reclamada o un 500 sin código propio: el handler pudo haber guardado antes de
        // fallar, así que no se sabe si el envío se hizo. El texto lo dice tal cual.
        IdempotencyKeyReused or InternalErrorCode      => "Tokens.Error.DistributionRepeated",
        Busy                                           => "Tokens.Error.DistributionBusy",
        TokenDistributionErrors.InvalidRecipient       => "Tokens.Error.SelfRecipient",
        TokenDistributionErrors.IdempotencyUnavailable => "Tokens.Error.DistributionUnavailable",
        _                                              => ErrorKeyFor(code)
    };

    /// <summary>
    /// ¿Se conserva la clave de la distribución? Sí cuando no se movió nada y se puede repetir lo mismo
    /// (candado ocupado, caché caída al reclamar la clave) o cuando no se sabe si se hizo (cualquier
    /// 5xx, con o sin código, como el <c>INTERNAL_ERROR</c> de una excepción en el handler; la caída de
    /// red la trata la pantalla): repetir con la misma clave no mueve los códigos dos veces (el BizCenter
    /// la tiene reclamada y contesta IDEMPOTENCY_KEY_REUSED). Tras un éxito o un fallo definitivo
    /// (destinatario, saldo, clave ya usada) se renueva.
    /// </summary>
    public static bool DistributionKeepsKey(string? code, HttpStatusCode status) =>
        code is Busy or TokenDistributionErrors.IdempotencyUnavailable
        || (int)status >= 500;

    /// <summary>Texto de una compra en cripto que salió sin pagar. Null para los demás estados.</summary>
    public static string? StatusKeyFor(string? status) => status switch
    {
        TokenPurchaseStatuses.Cancelled => "Tokens.Status.Cancelled",
        TokenPurchaseStatuses.Rejected  => "Tokens.Status.Rejected",
        TokenPurchaseStatuses.Expired   => "Tokens.Status.Expired",
        _                               => null
    };
}
