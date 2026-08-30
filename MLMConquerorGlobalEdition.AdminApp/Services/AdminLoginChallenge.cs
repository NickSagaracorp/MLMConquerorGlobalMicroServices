namespace MLMConquerorGlobalEdition.AdminApp.Services;

/// <summary>
/// El reto del segundo factor mientras dura: desde que lo emite el login hasta que la pantalla del
/// código lo canjea.
/// </summary>
/// <remarks>
/// EN MEMORIA Y NO EN EL ALMACENAMIENTO SEGURO, a propósito. El reto es una credencial a medio
/// camino —lleva el <c>sub</c> del usuario dentro— y vive cinco minutos; escribirlo en disco lo
/// dejaría ahí después de que caducara, sobreviviendo al cierre de la aplicación, a cambio de nada:
/// si el usuario cierra la aplicación en mitad del segundo factor, lo correcto es que vuelva a
/// empezar por la pantalla de entrada.
///
/// Es el equivalente de la cookie HttpOnly de reto que usan los portales web. La misma idea: el reto
/// no lo escribe ni lo ve el usuario, solo el código de seis dígitos.
/// </remarks>
public sealed class AdminLoginChallenge
{
    /// <summary>El reto emitido por el login, o null si no hay ninguno a medias.</summary>
    public string? ChallengeToken { get; private set; }

    /// <summary>A dónde fue el código, ya enmascarado por la API. Solo para enseñarlo.</summary>
    public string? MaskedTarget { get; private set; }

    /// <summary>Canal por el que se envió: <c>Email</c>, <c>Sms</c> o <c>Authenticator</c>.</summary>
    public string? Channel { get; private set; }

    public bool IsPending => !string.IsNullOrWhiteSpace(ChallengeToken);

    public void Start(string challengeToken, string? maskedTarget, string? channel)
    {
        ChallengeToken = challengeToken;
        MaskedTarget   = maskedTarget;
        Channel        = channel;
    }

    /// <summary>
    /// Sustituye el reto por el que devuelve un reenvío. El anterior queda gastado en la API, así
    /// que quedarse con él dejaría el siguiente canje condenado a fallar.
    /// </summary>
    public void Renew(string challengeToken, string? maskedTarget, string? channel)
        => Start(challengeToken, maskedTarget, channel);

    public void Clear()
    {
        ChallengeToken = null;
        MaskedTarget   = null;
        Channel        = null;
    }
}
