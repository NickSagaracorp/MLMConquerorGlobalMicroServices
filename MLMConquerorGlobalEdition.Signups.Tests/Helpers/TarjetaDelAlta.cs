using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

/// <summary>
/// La configuración del interruptor <c>Signup:CardEnabled</c> para las pruebas. La tarjeta del alta va
/// apagada por defecto: una prueba que necesita que la tarjeta complete el alta la enciende a propósito.
/// </summary>
internal static class TarjetaDelAlta
{
    public static IConfiguration Encendida() => Con("true");

    public static IConfiguration Apagada() => Con("false");

    /// <summary>Sin la clave: tiene que valer lo mismo que apagada.</summary>
    public static IConfiguration SinOpcion() => new ConfigurationBuilder().Build();

    public static IConfiguration Con(string? valor) => Interruptor.Con(SignupCardPayments.EnabledConfigKey, valor);
}

/// <summary>La configuración del interruptor <c>Membership:UpgradeEnabled</c> para las pruebas.</summary>
internal static class SubidaDeNivel
{
    public static IConfiguration Encendida() => Con("true");

    public static IConfiguration Apagada() => Con("false");

    public static IConfiguration Con(string? valor) => Interruptor.Con(MembershipUpgrades.EnabledConfigKey, valor);
}

internal static class Interruptor
{
    public static IConfiguration Con(string clave, string? valor)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [clave] = valor })
            .Build();
}
