using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Authn.Abstractions;

/// <summary>
/// Una sola superficie para los tres canales de 2FA: elige el canal, genera y despacha el
/// código, y lo verifica. Quien llama no distingue entre aplicación de autenticación, correo
/// y SMS — solo pide un challenge y luego lo redime.
///
/// Encima del challenge firmado (<see cref="IChallengeTokenService"/>, que es stateless) monta
/// lo que exige estado compartido: el antirreplay del challenge y del código TOTP, el límite de
/// intentos por challenge y el límite de emisiones por usuario.
///
/// Códigos de error: <c>CHANNEL_UNAVAILABLE</c>, <c>TOO_MANY_REQUESTS</c>,
/// <c>TOO_MANY_ATTEMPTS</c>, <c>CODE_INVALID</c>, más los de
/// <see cref="IChallengeTokenService"/> (<c>INVALID_CHALLENGE</c>, <c>CODE_EXPIRED</c>).
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// Emite un challenge y despacha el código por el canal elegido. El challenge solo se
    /// devuelve si el despacho tuvo éxito: si el transporte falla, el usuario recibe
    /// <c>CHANNEL_UNAVAILABLE</c> y la interfaz puede ofrecerle otro canal, en vez de dejarlo
    /// esperando un código que nunca va a llegar.
    /// </summary>
    /// <param name="forcedChannel">Canal impuesto por quien llama. Si es null se usa el
    /// preferido del usuario.</param>
    /// <param name="languageCode">
    /// Idioma de la plantilla. Llega como parámetro y no se resuelve aquí: quien llama ya
    /// conoce el idioma del usuario (<c>MemberProfile.DefaultLanguage</c>) y repetir esa
    /// consulta obligaría a esta librería a conocer el modelo de miembros. Null cae a "en".
    /// </param>
    Task<Result<ChallengeIssued>> IssueAsync(
        ApplicationUser   user,
        TwoFactorPurpose  purpose,
        string?           operationKey = null,
        TwoFactorChannel? forcedChannel = null,
        string?           languageCode = null,
        CancellationToken ct = default);

    /// <summary>
    /// Comprueba el código contra el challenge. Con éxito, marca el challenge como consumido
    /// para que no pueda redimirse otra vez dentro de su ventana de vida.
    /// </summary>
    Task<Result<ChallengeClaims>> VerifyAsync(
        string            challengeToken,
        string            code,
        TwoFactorPurpose  expectedPurpose,
        string?           expectedOperationKey = null,
        CancellationToken ct = default);
}
