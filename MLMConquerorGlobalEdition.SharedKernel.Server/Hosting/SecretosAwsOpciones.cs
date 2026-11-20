using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;

/// <summary>
/// La sección <c>SecretosAws</c> del appsettings.json de cada servicio: si se leen secretos de
/// AWS Secrets Manager, de qué entorno y cuáles. No es secreta: la lista de cada servicio queda a
/// la vista y su rol de AWS solo puede leer esos secretos.
/// </summary>
public sealed record SecretosAwsOpciones(
    bool Habilitado, string Entorno, string Region, IReadOnlyList<string> Secretos)
{
    public const string Seccion = "SecretosAws";

    /// <summary>Fijo a propósito: un prefijo configurable sería otra forma de leer lo ajeno.</summary>
    public const string Prefijo = "mlmconqueror";

    private static readonly HashSet<string> EntornosValidos = new(StringComparer.Ordinal)
        { "local", "staging", "prod" };

    private static readonly Regex NombreValido = new("^[a-z0-9-]+$", RegexOptions.CultureInvariant);

    public static SecretosAwsOpciones Leer(IConfiguration config)
    {
        var s = config.GetSection(Seccion);
        return new SecretosAwsOpciones(
            s.GetValue<bool>("Habilitado"),
            (s["Entorno"] ?? string.Empty).Trim(),
            (s["Region"] ?? string.Empty).Trim(),
            s.GetSection("Secretos").GetChildren()
                .Select(c => c.Value ?? string.Empty)
                .Where(v => v.Length > 0)
                .ToArray());
    }

    public string NombreCompleto(string secreto) => $"{Prefijo}/{Entorno}/{secreto}";

    public void Validar()
    {
        if (!EntornosValidos.Contains(Entorno))
            throw new InvalidOperationException(
                $"[SecretosAws] Entorno '{Entorno}' no válido: tiene que ser local, staging o prod.");

        if (Region.Length == 0)
            throw new InvalidOperationException("[SecretosAws] Falta SecretosAws:Region.");

        foreach (var secreto in Secretos)
            if (!NombreValido.IsMatch(secreto))
                throw new InvalidOperationException(
                    $"[SecretosAws] '{secreto}' no es un nombre de secreto válido: solo minúsculas, " +
                    "números y guiones, sin '/'.");
    }
}
