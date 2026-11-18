using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.ClientCore.AccessTokens;

/// <summary>Emisor y audiencia que tiene que traer un token de acceso, y el margen de reloj.</summary>
/// <param name="ClockSkew">
/// Dos minutos: el reloj de un teléfono no es el del servidor, y sin margen un token recién emitido
/// podría parecer "aún no válido". La API valida con su propio reloj en cada llamada.
/// </param>
public sealed record AccessTokenTrust(string Issuer, string Audience, TimeSpan ClockSkew)
{
    /// <summary>Los valores de <c>Jwt:Issuer</c> y <c>Jwt:Audience</c> de todas las API.</summary>
    public static readonly AccessTokenTrust Default =
        new("MLMConquerorGlobalEdition", "MLMConquerorGlobalEdition.Clients", TimeSpan.FromMinutes(2));
}

/// <summary>
/// Comprueba en una aplicación cliente el token de acceso antes de armar con él la identidad.
/// </summary>
public sealed class ClientAccessTokenValidator
{
    private readonly JwksKeySource    _keys;
    private readonly AccessTokenTrust _trust;

    public ClientAccessTokenValidator(JwksKeySource keys, AccessTokenTrust trust)
    {
        _keys  = keys;
        _trust = trust;
    }

    /// <summary>
    /// El resultado de la comprobación. Si ninguna llave conocida firmó el token, se vuelven a
    /// pedir las llaves una vez —puede que la API haya rotado— y se reintenta.
    /// </summary>
    public async Task<AccessTokenCheck> CheckAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return AccessTokenCheck.InvalidResult;

        var keys  = await _keys.GetKeysAsync(ct);
        var check = SignedAccessToken.CheckAccess(token, keys, _trust.Issuer, _trust.Audience, _trust.ClockSkew);

        if (check.Status == AccessTokenStatus.UnknownSigner && await _keys.TryRefreshAsync(ct))
        {
            keys  = await _keys.GetKeysAsync(ct);
            check = SignedAccessToken.CheckAccess(token, keys, _trust.Issuer, _trust.Audience, _trust.ClockSkew);
        }

        return check;
    }
}

public static class AccessTokenValidationRegistration
{
    /// <summary>
    /// Registra la validación del token de acceso para una aplicación cliente.
    /// </summary>
    /// <param name="authApiBaseAddress">La dirección de SignupAPI, que publica las llaves.</param>
    /// <param name="cache">Dónde guardar la copia de las llaves; en MAUI, las Preferences.</param>
    public static IServiceCollection AddAccessTokenValidation(
        this IServiceCollection services,
        string                  authApiBaseAddress,
        IJwksCache              cache,
        AccessTokenTrust?       trust = null)
    {
        services.AddHttpClient(JwksKeySource.HttpClientName, client =>
            client.BaseAddress = new Uri(authApiBaseAddress.TrimEnd('/') + "/"));

        services.AddSingleton(cache);
        services.AddSingleton(trust ?? AccessTokenTrust.Default);
        services.AddSingleton<JwksKeySource>();
        services.AddSingleton<ClientAccessTokenValidator>();
        return services;
    }
}
