namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Se ejecuta al arrancar SignupAPI. Fuera de Development, Entrax encendido sin credenciales o sin
/// callback https NO arranca: un alta que cobra sin poder verificar el callback, o con un callback
/// que Entrax no puede alcanzar, es peor que no ofrecer el método. El fake fuera de Development
/// tampoco arranca, esté o no encendido.
/// </summary>
public static class EntraxOptionsGuard
{
    public static IReadOnlyList<string> Problems(EntraxOptions o, bool isDevelopment)
    {
        var problems = new List<string>();

        if (o.UseFake && !isDevelopment)
            problems.Add("Entrax:UseFake solo se permite en Development.");

        if (!o.Enabled || (o.UseFake && isDevelopment))
            return problems;

        if (string.IsNullOrWhiteSpace(o.KeyId))  problems.Add("Entrax:KeyId está vacío.");
        if (string.IsNullOrWhiteSpace(o.Secret)) problems.Add("Entrax:Secret está vacío.");
        if (!IsAbsoluteHttps(o.CallbackUrl))     problems.Add("Entrax:CallbackUrl debe ser una URL https absoluta.");
        if (!IsAbsoluteHttps(o.BaseUrl))         problems.Add("Entrax:BaseUrl debe ser una URL https absoluta.");

        return problems;
    }

    public static void ThrowIfInvalid(EntraxOptions o, bool isDevelopment)
    {
        var problems = Problems(o, isDevelopment);
        if (problems.Count == 0 || isDevelopment) return;

        throw new InvalidOperationException("[Entrax] Configuración inválida: " + string.Join(" ", problems));
    }

    private static bool IsAbsoluteHttps(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;
}
