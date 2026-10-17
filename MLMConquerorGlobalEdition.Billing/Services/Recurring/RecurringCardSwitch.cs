using Microsoft.Extensions.Configuration;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

/// <summary>
/// ¿Se cobran con tarjeta las renovaciones de membresía? Apagado por defecto (Plan K, revisión I1/I2): el
/// gateway de Spreedly es un stub y devuelve "éxitos" simulados. Apagado, ni el motor
/// (RecurringBillingProcessor) ni la renovación manual (RenewMembershipHandler) llaman al gateway: el
/// motor registra un intento fallido con <see cref="CardDisabledCode"/> y sigue la cadencia, y la
/// renovación manual responde ese código. Se enciende con el Plan I (3DS con Spreedly), y antes hay que
/// cerrar el cobro diferido (DelayedFallbackChargeJob no avanza el estado ni la suscripción). Se lee en
/// cada llamada: apagarlo no exige reiniciar.
/// </summary>
public static class RecurringCardSwitch
{
    public const string ConfigKey       = "Billing:Recurring:CardEnabled";
    public const string CardDisabledCode = "CARD_DISABLED";

    public static bool IsEnabled(IConfiguration? configuration) =>
        configuration?.GetValue<bool>(ConfigKey) == true;
}
