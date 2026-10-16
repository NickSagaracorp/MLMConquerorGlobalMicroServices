namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Un interruptor que falla cerrado: solo lo enciende un "true" (sin distinguir mayúsculas); vacío, ilegible,
/// ausente o sin configuración es apagado. Lo usan
/// <see cref="SignupCardPayments"/> y <see cref="MembershipUpgrades"/>. Se lee en cada llamada: apagarlo en caliente no exige reiniciar.
/// </summary>
internal static class ClosedSwitch
{
    public static bool IsOn(IConfiguration? configuration, string key)
        => configuration is not null
           && bool.TryParse(configuration[key], out var enabled)
           && enabled;
}
