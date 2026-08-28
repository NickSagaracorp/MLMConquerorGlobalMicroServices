using MLMConquerorGlobalEdition.Domain.Entities.Security;

namespace MLMConquerorGlobalEdition.Authn.Models;

/// <summary>Datos extraídos de un challenge ya validado.</summary>
/// <param name="Jti">Identificador único, usado para el antirreplay y la auditoría.</param>
/// <param name="CodeHash">SHA-256 del código enviado. Null cuando el canal es Authenticator:
/// ahí el código lo genera la aplicación del usuario y lo verifica Identity.</param>
public sealed record ChallengeClaims(
    string           Jti,
    string           UserId,
    string           Email,
    TwoFactorPurpose Purpose,
    string?          OperationKey,
    TwoFactorChannel Channel,
    string?          CodeHash,
    DateTime         IssuedAt,
    DateTime         ExpiresAt);
