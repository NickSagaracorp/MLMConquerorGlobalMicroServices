namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// El interruptor de la tarjeta en el alta: <c>Signup:CardEnabled</c>, APAGADO por defecto y si falta.
///
/// Hoy nada cobra una tarjeta en el alta: la tokenización del navegador es simulada y
/// <c>SpreedlyCardGatewayService</c> es un stub. Con la tarjeta encendida, CompleteSignup guarda la
/// tarjeta y activa la membresía y las comisiones SIN cobrar. Por eso va apagada hasta que exista el cobro
/// real con 3DS (bloqueante de producción, <c>docs/deployment/pruebas-de-seguridad.md</c>), igual que
/// <c>Billing:TokenPurchase:CardEnabled</c> en la compra de tokens.
///
/// Se lee en cada llamada: apagarla en caliente no exige reiniciar SignupAPI. Solo la enciende un
/// "true" (sin distinguir mayúsculas); vacía, ilegible o ausente es apagada.
/// </summary>
public static class SignupCardPayments
{
    public const string EnabledConfigKey = "Signup:CardEnabled";

    public static bool IsEnabled(IConfiguration? configuration)
        => configuration is not null
           && bool.TryParse(configuration[EnabledConfigKey], out var enabled)
           && enabled;
}
