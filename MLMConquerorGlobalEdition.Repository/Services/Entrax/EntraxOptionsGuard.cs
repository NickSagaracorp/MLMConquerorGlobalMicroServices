namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Se ejecuta al arrancar SignupAPI. Fuera de Development, Entrax encendido sin credenciales o sin
/// callback https NO arranca: un alta que cobra sin poder verificar el callback, o con un callback
/// que Entrax no puede alcanzar, es peor que no ofrecer el método. Apagado (Enabled=false) con
/// credenciales a medias o BaseUrl sin https tampoco: el barrido corre apagado para los pagos en
/// vuelo y necesita credenciales que funcionen. El fake fuera de Development tampoco arranca, esté
/// o no encendido.
/// </summary>
public static class EntraxOptionsGuard
{
    public static IReadOnlyList<string> Problems(EntraxOptions o, bool isDevelopment)
    {
        var problems = new List<string>();

        if (o.UseFake && !isDevelopment)
            problems.Add("Entrax:UseFake solo se permite en Development.");

        if (o.UseFake && isDevelopment)
            return problems;

        if (!o.Enabled)
        {
            // Apagar Entrax es dejar de ofrecerlo, no quitar credenciales: el barrido sigue
            // liquidando con Enabled=false los pagos en vuelo. Si hay alguna credencial, tiene que
            // estar completa y apuntar a https; si no hay ninguna, no se exige nada.
            if (string.IsNullOrWhiteSpace(o.KeyId) && string.IsNullOrWhiteSpace(o.Secret))
                return problems;

            if (string.IsNullOrWhiteSpace(o.KeyId))  problems.Add("Entrax:KeyId está vacío (hay Secret: credenciales a medias).");
            if (string.IsNullOrWhiteSpace(o.Secret)) problems.Add("Entrax:Secret está vacío (hay KeyId: credenciales a medias).");
            if (!IsAbsoluteHttps(o.BaseUrl))         problems.Add("Entrax:BaseUrl debe ser una URL https absoluta.");
            return problems;
        }

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
