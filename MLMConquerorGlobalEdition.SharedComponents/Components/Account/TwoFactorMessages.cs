namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account;

/// <summary>
/// Traduce a texto los códigos de error del segundo factor.
///
/// La API devuelve códigos (<c>CODE_INVALID</c>, <c>TOO_MANY_ATTEMPTS</c>…) y nunca la frase:
/// el mensaje que lee una persona lo decide la interfaz, que es la única capa que sabe a quién
/// le habla. Está en un sitio y no repartido por las dos páginas para que ambas digan lo mismo
/// ante el mismo código.
/// </summary>
public static class TwoFactorMessages
{
    /// <summary>Frase para <paramref name="errorCode"/>; genérica si el código no se reconoce.</summary>
    /// <remarks>
    /// El caso por defecto es deliberadamente vago: un código desconocido significa que la API
    /// devolvió algo que esta interfaz no previó, y adivinar la causa delante del usuario es
    /// peor que decirle que vuelva a intentarlo.
    /// </remarks>
    public static string ForErrorCode(string? errorCode) =>
        errorCode?.Trim().ToUpperInvariant() switch
        {
            "CODE_INVALID"        => "El código no es correcto. Revísalo e inténtalo de nuevo.",
            "CODE_EXPIRED"        => "El código caducó. Pide uno nuevo.",
            "INVALID_CHALLENGE"   => "La sesión de verificación no es válida. Vuelve a iniciar sesión.",
            "TOO_MANY_ATTEMPTS"   => "Demasiados intentos. Pide un código nuevo.",
            "TOO_MANY_REQUESTS"   => "Has pedido demasiados códigos. Espera unos minutos.",
            "CHANNEL_UNAVAILABLE" => "No pudimos enviarte el código. Inténtalo de nuevo en unos minutos.",
            _                     => "No pudimos verificar el código. Inténtalo de nuevo."
        };

    /// <summary>Confirmación tras un reenvío correcto.</summary>
    public const string ResentNotice = "Te enviamos un código nuevo.";

    /// <summary>
    /// Nombre del canal tal y como lo emite <c>TwoFactorChannel</c> en el reto. Se compara como
    /// texto y no contra el enum porque SharedComponents no referencia Domain.
    /// </summary>
    public const string AuthenticatorChannel = "Authenticator";

    /// <summary>¿El canal es la aplicación autenticadora?</summary>
    public static bool IsAuthenticator(string? channel) =>
        string.Equals(channel?.Trim(), AuthenticatorChannel, StringComparison.OrdinalIgnoreCase);
}
