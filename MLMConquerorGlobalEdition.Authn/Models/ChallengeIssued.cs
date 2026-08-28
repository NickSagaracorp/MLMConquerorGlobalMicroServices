using MLMConquerorGlobalEdition.Domain.Entities.Security;

namespace MLMConquerorGlobalEdition.Authn.Models;

/// <summary>Challenge recién emitido y ya despachado por su canal.</summary>
/// <param name="MaskedTarget">A dónde fue el código, enmascarado para poder enseñarlo:
/// correo o teléfono. Cadena vacía para Authenticator, donde no se envía nada.</param>
public sealed record ChallengeIssued(
    string           ChallengeToken,
    TwoFactorChannel Channel,
    string           MaskedTarget,
    DateTime         ExpiresAt);
