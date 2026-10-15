namespace MLMConquerorGlobalEdition.SharedKernel.Services;

/// <summary>
/// Cómo dejan rastro en el log los transportes nulos (<see cref="NullEmailService"/>,
/// <c>NullSmsService</c>) sin escribir lo que el mensaje lleva dentro.
/// </summary>
/// <remarks>
/// Los transportes nulos se eligen solos cuando falta la configuración del proveedor, así que no
/// viven solo en la máquina de un desarrollador: un despliegue a medio configurar los usa igual. Si
/// escriben las variables, el log pasa a contener los códigos de 2FA y de step-up, los enlaces de
/// restablecer contraseña y de confirmar correo — cualquiera con acceso de lectura a los logs entra
/// en cualquier cuenta. Por eso del log solo salen los NOMBRES de las variables, que es lo que sirve
/// para depurar una plantilla, y nunca el valor.
///
/// Para las variables sensibles se escribe además un marcador explícito, para que quien lea el log
/// sepa que el valor existía y se ocultó a propósito; las demás salen solo con su nombre. No se
/// intenta decidir qué valores son "inofensivos": una lista de permitidos se queda corta el día que
/// alguien añade una variable nueva a una plantilla, y aquí equivocarse es filtrar un secreto.
/// </remarks>
public static class NotificationLogRedaction
{
    /// <summary>Lo que se escribe en lugar del valor de una variable sensible.</summary>
    public const string Redacted = "[redacted]";

    /// <summary>
    /// Fragmentos que marcan una variable como sensible, se busquen donde se busquen en el nombre y
    /// sin distinguir mayúsculas: <c>Code</c>, <c>ResetUrl</c>, <c>ConfirmationLink</c>,
    /// <c>AccessToken</c>, <c>TemporaryPassword</c>…
    /// </summary>
    private static readonly string[] SensitiveFragments = ["code", "token", "link", "url", "password"];

    /// <summary>¿El nombre de esta variable apunta a un secreto?</summary>
    public static bool IsSensitive(string variableName) =>
        SensitiveFragments.Any(f => variableName.Contains(f, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Las variables tal como pueden ir al log: <c>Code=[redacted], ExpiresInMinutes</c>. Nunca un
    /// valor, sea o no sensible.
    /// </summary>
    public static string DescribeVariables(IReadOnlyDictionary<string, string>? variables) =>
        variables is null || variables.Count == 0
            ? "(none)"
            : string.Join(", ", variables.Keys.Select(k => IsSensitive(k) ? $"{k}={Redacted}" : k));

    /// <summary>
    /// Primera letra, asteriscos y el dominio. Basta para saber a qué cuenta iba el correo al
    /// depurar sin dejar en el log la dirección entera, que es PII.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(none)";
        var at = email.IndexOf('@');
        if (at <= 0) return new string('*', email.Length);
        return $"{email[0]}***{email[at..]}";
    }

    /// <summary>
    /// Solo los cuatro últimos dígitos. El teléfono es PII y además el factor por el que llegan los
    /// códigos: entero en un log, es medio camino para desviarlos.
    /// </summary>
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "(none)";
        var trimmed = phone.Trim();
        return trimmed.Length <= 4
            ? new string('*', trimmed.Length)
            : new string('*', trimmed.Length - 4) + trimmed[^4..];
    }
}
