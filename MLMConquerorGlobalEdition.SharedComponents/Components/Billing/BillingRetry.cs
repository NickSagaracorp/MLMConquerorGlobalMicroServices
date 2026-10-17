using System.Net;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Billing;

/// <summary>
/// La parte común de "¿se repite el intento con la MISMA clave?" para los pagos que pasan por Billing
/// (compra de tokens y cuota anual; revisión final del Plan K, M-c). Cada pantalla añade sus propios
/// códigos de "en curso"; aquí está lo que vale para todas: el permiso de step-up rechazado (no se
/// ejecutó nada y la repetición, con un código nuevo, debe ser el mismo pago) y un 5xx cuyo resultado no
/// se conoce.
/// </summary>
public static class BillingRetry
{
    /// <summary>
    /// 5xx que prueban que Billing no hizo nada (contrato roto, sesión que no casa, servicio de pago
    /// apagado): cierran la clave. Cualquier otro 5xx, con o sin código, la conserva.
    /// </summary>
    public static readonly IReadOnlySet<string> DefinitiveServerErrors = new HashSet<string>(StringComparer.Ordinal)
    {
        TokenPurchaseErrors.BillingContractError,
        TokenPurchaseErrors.BillingAuthMismatch,
        TokenPurchaseErrors.PaymentServiceUnavailable
    };

    /// <summary>¿El resultado del intento no se conoce o no se llegó a ejecutar por el step-up?</summary>
    public static bool UnknownOrNotExecuted(string? code, HttpStatusCode status) =>
        code is StepUpGrant.RequiredErrorCode or StepUpGrant.InvalidErrorCode
        || ((int)status >= 500 && (string.IsNullOrEmpty(code) || !DefinitiveServerErrors.Contains(code)));
}
