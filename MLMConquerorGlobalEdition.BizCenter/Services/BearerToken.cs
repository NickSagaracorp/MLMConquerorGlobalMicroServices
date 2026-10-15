using Microsoft.Net.Http.Headers;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <summary>El token crudo de Authorization, para reenviarlo a otro servicio. Vacío si no hay Bearer.</summary>
public static class BearerToken
{
    public static string From(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(HeaderNames.Authorization, out var values))
            return string.Empty;

        var raw = values.ToString();
        const string prefix = "Bearer ";
        return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? raw.Substring(prefix.Length).Trim()
            : string.Empty;
    }
}
