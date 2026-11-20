namespace MLMConquerorGlobalEdition.SharedKernel.Security;

/// <summary>
/// Dónde y con qué nombre se guarda en disco un adjunto que sube un usuario (tickets del centro de
/// negocios y de administración). El nombre original solo se muestra: en disco va un Guid con una
/// extensión de la lista, y la ruta final se comprueba contra la carpeta antes de escribir.
/// </summary>
public static class AdjuntoEnDisco
{
    public const long TamanoMaximoBytes = 10_000_000; // 10 MB

    public static readonly IReadOnlySet<string> ExtensionesPermitidas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp",
            ".pdf",
            ".doc", ".docx",
            ".txt"
        };

    /// <summary>
    /// La extensión del nombre original, en minúsculas, si está en la lista; <c>null</c> si no la
    /// tiene o no está permitida.
    /// </summary>
    public static string? ExtensionPermitida(string? nombreOriginal)
    {
        if (string.IsNullOrWhiteSpace(nombreOriginal)) return null;
        var ext = Path.GetExtension(Path.GetFileName(nombreOriginal)).ToLowerInvariant();
        return ext.Length > 0 && ExtensionesPermitidas.Contains(ext) ? ext : null;
    }

    /// <summary>Un nombre nuevo para disco: Guid más la extensión ya validada.</summary>
    public static string NombreNuevo(string extensionPermitida)
    {
        if (!ExtensionesPermitidas.Contains(extensionPermitida))
            throw new ArgumentException("Extensión no permitida.", nameof(extensionPermitida));
        return $"{Guid.NewGuid():N}{extensionPermitida.ToLowerInvariant()}";
    }

    /// <summary>
    /// La ruta completa de <paramref name="nombreEnDisco"/> dentro de <paramref name="carpeta"/>.
    /// Falla si el nombre trae separadores o si la ruta resultante sale de la carpeta.
    /// </summary>
    public static string RutaDentroDe(string carpeta, string nombreEnDisco)
    {
        if (string.IsNullOrWhiteSpace(nombreEnDisco)
            || nombreEnDisco != Path.GetFileName(nombreEnDisco)
            || nombreEnDisco.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || nombreEnDisco is "." or "..")
            throw new InvalidOperationException("Nombre de archivo no válido para disco.");

        var raiz = Path.GetFullPath(carpeta);
        if (!raiz.EndsWith(Path.DirectorySeparatorChar)) raiz += Path.DirectorySeparatorChar;

        var ruta = Path.GetFullPath(Path.Combine(raiz, nombreEnDisco));
        if (!ruta.StartsWith(raiz, StringComparison.Ordinal))
            throw new InvalidOperationException("La ruta del adjunto sale de su carpeta.");

        return ruta;
    }
}
