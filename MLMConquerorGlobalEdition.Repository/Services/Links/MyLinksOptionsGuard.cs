using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.Repository.Services.Links;

/// <summary>
/// Valida las plantillas al arrancar. Una sola regla mala impide arrancar el BizCenter: es mejor que
/// repartir a miles de miembros un enlace http, con usuario y contraseña, o con el slug en el dominio
/// (que convertiría cada slug en un subdominio que no controlamos).
/// </summary>
public static class MyLinksOptionsGuard
{
    private const int MaxLabelLength = 60;

    private static readonly Regex KeyPattern = new(@"^[A-Za-z0-9-]{1,40}$", RegexOptions.Compiled);

    /// <summary>El primer problema encontrado, o null si todo es válido.</summary>
    public static string? Error(MyLinksOptions options)
    {
        if (options.Links.Count > MyLinksOptions.MaxLinks)
            return $"[MyLinks] Como mucho {MyLinksOptions.MaxLinks} enlaces.";

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in options.Links)
        {
            var key  = link.Key ?? string.Empty;
            var name = key.Length == 0 ? "(sin clave)" : key;

            if (!KeyPattern.IsMatch(key))
                return $"[MyLinks] '{name}': la clave solo admite letras, números y guiones (de 1 a 40).";
            if (!keys.Add(key))
                return $"[MyLinks] '{name}': clave repetida.";
            if (string.IsNullOrWhiteSpace(link.Label) || link.Label.Length > MaxLabelLength)
                return $"[MyLinks] '{name}': la etiqueta es obligatoria y de {MaxLabelLength} caracteres como mucho.";

            var urlError = UrlError(link.Url ?? string.Empty);
            if (urlError is not null)
                return $"[MyLinks] '{name}': {urlError}";
        }

        return null;
    }

    public static void ThrowIfInvalid(MyLinksOptions options)
    {
        var error = Error(options);
        if (error is not null) throw new InvalidOperationException(error);
    }

    private static string? UrlError(string url)
    {
        var tokens = CountOf(url, MyLinksOptions.SlugToken);
        if (tokens > 1)
            return "{slug} solo puede aparecer una vez.";

        var probe = url.Replace(MyLinksOptions.SlugToken, "slug", StringComparison.Ordinal);
        if (probe.Contains('{') || probe.Contains('}'))
            return "solo se admite el marcador {slug}.";

        if (!Uri.TryCreate(probe, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return "tiene que ser una URL https absoluta.";

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return "no puede llevar usuario ni contraseña.";

        if (tokens == 1)
        {
            // El primer '/' después de "https://" cierra el dominio: el slug tiene que ir detrás.
            var authorityEnd = url.IndexOf('/', "https://".Length);
            var at           = url.IndexOf(MyLinksOptions.SlugToken, StringComparison.Ordinal);
            if (authorityEnd < 0 || at < authorityEnd)
                return "{slug} tiene que ir en la ruta, no en el dominio.";
        }

        return null;
    }

    private static int CountOf(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }
}
