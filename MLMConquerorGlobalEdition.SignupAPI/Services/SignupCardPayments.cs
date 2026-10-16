namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// El interruptor de la tarjeta en el alta: <c>Signup:CardEnabled</c>, APAGADO por defecto y si falta.
///
/// Hoy nada cobra una tarjeta en el alta: la tokenización del navegador es simulada y
/// <c>SpreedlyCardGatewayService</c> es un stub. Con la tarjeta encendida, CompleteSignup guarda la
/// tarjeta y activa la membresía y las comisiones SIN cobrar. Por eso va apagada hasta que exista el cobro
/// real con 3DS (bloqueante de producción, <c>docs/deployment/pruebas-de-seguridad.md</c>), igual que
/// <c>Billing:TokenPurchase:CardEnabled</c> en la compra de tokens.
/// </summary>
public static class SignupCardPayments
{
    public const string EnabledConfigKey = "Signup:CardEnabled";

    public static bool IsEnabled(IConfiguration? configuration) => ClosedSwitch.IsOn(configuration, EnabledConfigKey);
}

/// <summary>
/// El interruptor de la subida de nivel del miembro: <c>Membership:UpgradeEnabled</c>, APAGADO por defecto y
/// si falta. <c>UpgradeMembershipHandler</c> cambia de nivel sin cobrar nada; lo encenderá el Plan G cuando
/// la subida cobre la diferencia.
/// </summary>
public static class MembershipUpgrades
{
    public const string EnabledConfigKey = "Membership:UpgradeEnabled";

    public static bool IsEnabled(IConfiguration? configuration) => ClosedSwitch.IsOn(configuration, EnabledConfigKey);
}

/// <summary>
/// Un interruptor que falla cerrado: solo lo enciende un "true" (sin distinguir mayúsculas); vacío, ilegible,
/// ausente o sin configuración es apagado. Se lee en cada llamada: apagarlo en caliente no exige reiniciar.
/// </summary>
internal static class ClosedSwitch
{
    public static bool IsOn(IConfiguration? configuration, string key)
        => configuration is not null
           && bool.TryParse(configuration[key], out var enabled)
           && enabled;
}
