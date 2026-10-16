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

    public static IConfiguration Con(string? valor)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [SignupCardPayments.EnabledConfigKey] = valor })
            .Build();
}
