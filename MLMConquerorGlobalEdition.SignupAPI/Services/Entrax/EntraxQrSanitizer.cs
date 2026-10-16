namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

/// <summary>
/// Lo que Entrax devuelve para pintar (imagen y enlace) acaba en un img src y en un href de la
/// pantalla del alta. Solo pasa un PNG en base64 y un enlace https; lo demás se descarta.
/// </summary>
public static class EntraxQrSanitizer
{
    private const string PngPrefix = "data:image/png;base64,";
    public const int MaxImageLength = 200_000;

    public static string? SafeImage(string? dataUri)
    {
        if (string.IsNullOrEmpty(dataUri)
            || dataUri.Length > MaxImageLength
            || dataUri.Length == PngPrefix.Length
            || !dataUri.StartsWith(PngPrefix, StringComparison.Ordinal))
            return null;

        var payload = dataUri.AsSpan(PngPrefix.Length);
        var buffer = new byte[payload.Length];
        return Convert.TryFromBase64Chars(payload, buffer, out _) ? dataUri : null;
    }

    public static string? SafeLink(string? link)
        => Uri.TryCreate(link, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps ? link : null;
}
