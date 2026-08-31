namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

public interface IJwtService
{
    /// <summary>Generates a JWT access token for the given claims.</summary>
    /// <param name="impersonationReadOnly">
    /// Restringe el token a lectura. La restricción viaja DENTRO del token —no en el cuerpo de la
    /// respuesta— porque quien la tiene que aplicar es el servidor que recibe el token, no la
    /// interfaz que lo guarda. Ver <c>ImpersonationScope</c>.
    /// </param>
    string GenerateAccessToken(
        string userId,
        string memberId,
        string email,
        IEnumerable<string> roles,
        bool isImpersonating = false,
        string? impersonatedBy = null,
        string? defaultLanguage = null,
        bool impersonationReadOnly = false);

    /// <summary>Generates a cryptographically random refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>Returns the expiry duration for access tokens (from config).</summary>
    TimeSpan AccessTokenExpiry { get; }

    /// <summary>Returns the expiry duration for refresh tokens (from config).</summary>
    TimeSpan RefreshTokenExpiry { get; }
}
