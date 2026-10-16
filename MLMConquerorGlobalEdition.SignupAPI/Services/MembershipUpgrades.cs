namespace MLMConquerorGlobalEdition.SignupAPI.Services;

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
