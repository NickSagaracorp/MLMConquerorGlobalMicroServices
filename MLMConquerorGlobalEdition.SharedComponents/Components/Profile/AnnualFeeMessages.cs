using System.Net;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Profile;

/// <summary>
/// Qué texto enseña la tarjeta de la cuota para cada código de error (Plan K) y si un fallo deja el
/// intento pendiente (misma clave al repetir). Nunca se enseña el texto crudo del servidor.
/// </summary>
public static class AnnualFeeMessages
{
    public static string ErrorKeyFor(string? errorCode) => errorCode?.Trim().ToUpperInvariant() switch
    {
        AnnualFeeErrors.NotDue                        => "AnnualFee.Error.NotDue",
        AnnualFeeErrors.AlreadyPaid                   => "AnnualFee.Error.AlreadyPaid",
        AnnualFeeErrors.InProgress                    => "AnnualFee.Error.InProgress",
        AnnualFeeErrors.NeedsReview                   => "AnnualFee.Error.NeedsReview",
        AnnualFeeErrors.NoFeeToken                    => "AnnualFee.Error.NoToken",
        AnnualFeeErrors.InsufficientCommissionBalance => "AnnualFee.Error.InsufficientCommissions",
        AnnualFeeErrors.InsufficientEWalletBalance
            or AnnualFeeErrors.EWalletNotFound
            or AnnualFeeErrors.EWalletUnavailable
            or AnnualFeeErrors.EWalletRejected        => "AnnualFee.Error.EWallet",
        AnnualFeeErrors.Waived                        => "AnnualFee.Error.Waived",
        AnnualFeeErrors.AccountNotActive              => "AnnualFee.Error.AccountNotActive",
        AnnualFeeErrors.MembershipOnHold              => "AnnualFee.Error.MembershipOnHold",
        AnnualFeeErrors.Busy                          => "AnnualFee.Error.Busy",
        AnnualFeeErrors.Impersonation
            or ImpersonationScope.ReadOnlyErrorCode   => "AnnualFee.Error.Impersonation",
        StepUpGrant.RequiredErrorCode
            or StepUpGrant.InvalidErrorCode
            or StepUpGrant.CooldownErrorCode          => "AnnualFee.Error.StepUp",
        TokenPurchaseErrors.BillingUnavailable        => "AnnualFee.Error.Unavailable",
        _                                             => "AnnualFee.Error.Generic"
    };

    /// <summary>
    /// ¿El intento quedó sin resolver? En curso, ocupado o Billing sin contestar, más la regla común con
    /// la compra de tokens (BillingRetry: step-up rechazado o un 5xx de resultado desconocido): se repite
    /// con la MISMA clave y Billing devuelve lo que ya hizo. EN REVISIÓN NO (revisión final, M-b): hay un
    /// pago a conciliar para el ciclo y repetir no sirve; la tarjeta vuelve a leer el estado. Lo demás es
    /// definitivo y el siguiente intento lleva otra clave.
    /// </summary>
    public static bool KeepsKey(string? errorCode, HttpStatusCode status) =>
        errorCode is AnnualFeeErrors.InProgress or AnnualFeeErrors.Busy or TokenPurchaseErrors.BillingUnavailable
        || Billing.BillingRetry.UnknownOrNotExecuted(errorCode, status);
}
