namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account;

/// <summary>
/// Traduce los códigos de error del segundo factor a claves de <c>SharedResources</c>.
///
/// La API devuelve códigos (<c>CODE_INVALID</c>, <c>TOO_MANY_ATTEMPTS</c>…) y nunca la frase: el
/// texto que lee una persona sale del <c>.resx</c>, que es lo que hace que la pantalla hable el
/// idioma del portal. Lo que queda aquí es solo el mapeo código→clave, en un sitio y no repartido
/// por las dos páginas, para que ambas digan lo mismo ante el mismo código.
/// </summary>
public static class TwoFactorMessages
{
    /// <summary>Clave de recurso para <paramref name="errorCode"/>; genérica si no se reconoce.</summary>
    /// <remarks>
    /// El caso por defecto es deliberadamente vago: un código desconocido significa que la API
    /// devolvió algo que esta interfaz no previó, y adivinar la causa delante del usuario es
    /// peor que decirle que vuelva a intentarlo.
    /// </remarks>
    public static string ErrorKeyFor(string? errorCode) =>
        errorCode?.Trim().ToUpperInvariant() switch
        {
            "CODE_INVALID"        => "TwoFactor.Error.CodeInvalid",
            "CODE_EXPIRED"        => "TwoFactor.Error.CodeExpired",
            "INVALID_CHALLENGE"   => "TwoFactor.Error.InvalidChallenge",
            "TOO_MANY_ATTEMPTS"   => "TwoFactor.Error.TooManyAttempts",
            "TOO_MANY_REQUESTS"   => "TwoFactor.Error.TooManyRequests",
            "CHANNEL_UNAVAILABLE" => "TwoFactor.Error.ChannelUnavailable",
            _                     => "TwoFactor.Error.Generic"
        };

    /// <summary>
    /// Nombres de canal tal y como los emite <c>TwoFactorChannel</c> en el reto. Se comparan como
    /// texto y no contra el enum porque SharedComponents no referencia Domain.
    /// </summary>
    public const string AuthenticatorChannel = "Authenticator";

    /// <inheritdoc cref="AuthenticatorChannel"/>
    public const string SmsChannel = "Sms";

    /// <summary>¿El canal es la aplicación autenticadora?</summary>
    public static bool IsAuthenticator(string? channel) =>
        string.Equals(channel?.Trim(), AuthenticatorChannel, StringComparison.OrdinalIgnoreCase);

    /// <summary>¿El canal es el SMS?</summary>
    public static bool IsSms(string? channel) =>
        string.Equals(channel?.Trim(), SmsChannel, StringComparison.OrdinalIgnoreCase);
}
