using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;

namespace MLMConquerorGlobalEdition.ClientCore.AccessTokens;

/// <summary>Qué salió de comprobar un token.</summary>
public enum AccessTokenStatus
{
    /// <summary>Firmado por nuestro emisor, para esta audiencia y vigente.</summary>
    Valid,

    /// <summary>Firmado por nuestro emisor pero ya caducado. La firma se comprueba antes que la vigencia.</summary>
    Expired,

    /// <summary>
    /// Ninguna de las llaves conocidas lo firmó. Puede ser un token alterado o una llave nueva que
    /// todavía no se descargó: quien tenga cómo, vuelve a pedir las llaves y lo intenta otra vez.
    /// </summary>
    UnknownSigner,

    /// <summary>Ilegible, de otro emisor u otra audiencia, o un reto de 2FA usado como acceso.</summary>
    Invalid
}

/// <summary>El resultado y, solo si la firma es buena, el token con sus claims tal como vienen.</summary>
public sealed record AccessTokenCheck(AccessTokenStatus Status, JwtSecurityToken? Token)
{
    public static readonly AccessTokenCheck InvalidResult = new(AccessTokenStatus.Invalid, null);

    public bool IsValid => Status == AccessTokenStatus.Valid && Token is not null;
}

/// <summary>
/// La única comprobación de tokens del lado cliente: la usan las dos MAUI y los dos portales web.
/// </summary>
/// <remarks>
/// Antes los cuatro LEÍAN el token (<c>ReadJwtToken</c>) y armaban con sus claims la identidad
/// —roles incluidos— sin mirar quién lo había firmado. La API valida la firma en cada llamada, así
/// que un token alterado no autorizaba nada en el servidor, pero sí cambiaba lo que la interfaz
/// mostraba. Aquí se exige firma RS256 de una de nuestras llaves, emisor, audiencia y vigencia.
///
/// Los claims se devuelven sin el mapeo de nombres de .NET (<c>MapInboundClaims = false</c>), igual
/// que los devolvía <c>ReadJwtToken</c>, para que nadie que los lea tenga que cambiar.
/// </remarks>
public static class SignedAccessToken
{
    /// <summary>
    /// Comprueba un token de acceso. Un reto de 2FA no pasa aunque esté bien firmado: lleva otra
    /// audiencia y además el claim de propósito.
    /// </summary>
    public static AccessTokenCheck CheckAccess(
        string? token, IEnumerable<SecurityKey> keys, string issuer, string audience, TimeSpan clockSkew)
    {
        var check = Check(token, keys, issuer, audience, clockSkew, validateLifetime: true);
        return check.Token is not null && ChallengeAudience.CarriesPurpose(check.Token.Claims)
            ? AccessTokenCheck.InvalidResult
            : check;
    }

    /// <summary>
    /// Comprueba un reto de 2FA (audiencia <c>{audiencia}.challenge</c> y claim de propósito) sin
    /// mirar la vigencia: quien lo usa solo decide qué texto mostrar, y un reto caducado tiene que
    /// poder enseñar su pantalla para ofrecer el reenvío. Canjearlo sigue siendo cosa de la API.
    /// </summary>
    public static AccessTokenCheck CheckChallenge(
        string? token, IEnumerable<SecurityKey> keys, string issuer, string accessAudience)
    {
        var check = Check(token, keys, issuer, ChallengeAudience.For(accessAudience),
            TimeSpan.Zero, validateLifetime: false);
        return check.Token is not null && !ChallengeAudience.CarriesPurpose(check.Token.Claims)
            ? AccessTokenCheck.InvalidResult
            : check;
    }

    private static AccessTokenCheck Check(
        string? token, IEnumerable<SecurityKey> keys, string issuer, string audience,
        TimeSpan clockSkew, bool validateLifetime)
    {
        if (string.IsNullOrWhiteSpace(token)) return AccessTokenCheck.InvalidResult;

        var signingKeys = keys.ToList();
        if (signingKeys.Count == 0) return new AccessTokenCheck(AccessTokenStatus.UnknownSigner, null);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = validateLifetime,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens      = true,
            RequireExpirationTime    = true,
            ValidIssuer              = issuer,
            ValidAudience            = audience,
            IssuerSigningKeys        = signingKeys,
            ValidAlgorithms          = [SecurityAlgorithms.RsaSha256],
            ClockSkew                = clockSkew
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            handler.ValidateToken(token, parameters, out var validated);
            return validated is JwtSecurityToken jwt
                ? new AccessTokenCheck(AccessTokenStatus.Valid, jwt)
                : AccessTokenCheck.InvalidResult;
        }
        // El handler comprueba la firma antes que la vigencia: si llega aquí, la firma era buena.
        catch (SecurityTokenExpiredException)
        {
            return new AccessTokenCheck(AccessTokenStatus.Expired, null);
        }
        catch (Exception ex) when (ex is SecurityTokenSignatureKeyNotFoundException
                                       or SecurityTokenInvalidSignatureException)
        {
            return new AccessTokenCheck(AccessTokenStatus.UnknownSigner, null);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return AccessTokenCheck.InvalidResult;
        }
    }
}
