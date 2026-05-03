namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

public interface IJwtService
{
    /// <summary>Generates a JWT access token for the given claims.</summary>
    string GenerateAccessToken(
        string userId,
        string memberId,
        string email,
        IEnumerable<string> roles,
        bool isImpersonating = false,
        string? impersonatedBy = null,
        string? defaultLanguage = null);

    /// <summary>Generates a cryptographically random refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>Returns the expiry duration for access tokens (from config).</summary>
    TimeSpan AccessTokenExpiry { get; }

    /// <summary>Returns the expiry duration for refresh tokens (from config).</summary>
    TimeSpan RefreshTokenExpiry { get; }
}
