using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Memory;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;

public static class SecretosAwsConfiguracion
{
    /// <summary>
    /// Lee de AWS Secrets Manager los secretos de la lista <c>SecretosAws:Secretos</c> de este
    /// servicio, en su entorno, y los mezcla en la configuración justo antes de las variables de
    /// entorno: ganan sobre appsettings.json y pierden contra una variable de entorno (para forzar
    /// un valor en una emergencia sin tocar AWS). Si uno falla, el servicio no arranca: nunca se
    /// sigue con el valor de appsettings.json.
    ///
    /// Se llama en Program.cs justo después de WebApplication.CreateBuilder, antes de que nada
    /// lea la configuración.
    /// </summary>
    public static ConfigurationManager AddSecretosDeAws(
        this ConfigurationManager config, ILectorDeSecretos? lector = null, TextWriter? log = null)
    {
        log ??= Console.Out;
        var opciones = SecretosAwsOpciones.Leer(config);

        if (!opciones.Habilitado)
        {
            log.WriteLine("[SecretosAws] Deshabilitado: se usan solo appsettings y variables de entorno.");
            return config;
        }

        opciones.Validar();
        if (opciones.Secretos.Count == 0) return config;

        var propio = lector is null;
        lector ??= new LectorDeSecretosAws(opciones.Region);
        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var secreto in opciones.Secretos)
            {
                var nombre = opciones.NombreCompleto(secreto);
                var claves = Interpretar(nombre, lector.Leer(nombre));
                foreach (var (clave, valor) in claves) valores[clave] = valor;
                log.WriteLine($"[SecretosAws] {nombre}: {claves.Count} clave(s).");
            }
        }
        finally
        {
            if (propio) (lector as IDisposable)?.Dispose();
        }

        var fuente = new MemoryConfigurationSource { InitialData = valores };
        var indice = UltimaFuenteDeVariablesDeEntorno(config);
        if (indice < 0) config.Sources.Add(fuente);
        else config.Sources.Insert(indice, fuente);

        return config;
    }

    /// <summary>
    /// WebApplication.CreateBuilder agrega varias fuentes de variables de entorno: las de
    /// DOTNET_/ASPNETCORE_ al principio (configuración del anfitrión) y la sin prefijo después de
    /// appsettings. La que importa es la última.
    /// </summary>
    private static int UltimaFuenteDeVariablesDeEntorno(ConfigurationManager config)
    {
        var fuentes = ((IConfigurationBuilder)config).Sources;
        for (var i = fuentes.Count - 1; i >= 0; i--)
            if (fuentes[i] is EnvironmentVariablesConfigurationSource) return i;
        return -1;
    }

    private static Dictionary<string, string> Interpretar(string nombre, string texto)
    {
        try
        {
            using var doc = JsonDocument.Parse(texto);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new SecretoNoDisponibleException(nombre, MotivoSecretoNoDisponible.FormatoInvalido);

            var claves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.String)
                    throw new SecretoNoDisponibleException(nombre, MotivoSecretoNoDisponible.FormatoInvalido);
                claves[p.Name.Replace("__", ConfigurationPath.KeyDelimiter)] = p.Value.GetString()!;
            }
            return claves;
        }
        catch (JsonException ex)
        {
            throw new SecretoNoDisponibleException(nombre, MotivoSecretoNoDisponible.FormatoInvalido, ex);
        }
    }
}
