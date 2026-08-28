using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Authn.Abstractions;

/// <summary>
/// Emite y valida el "challenge": un JWT de corta vida que lleva dentro el SHA-256 del código
/// de 6 dígitos que se envió al usuario. Es stateless — no hay tabla de códigos pendientes:
/// el código vuelve del cliente y se compara contra el hash que viaja firmado en el token.
///
/// El challenge se firma con el mismo par RSA que los tokens de acceso, pero el claim
/// <c>purpose</c> lo separa de ellos y, sobre todo, separa unos challenges de otros: un código
/// pedido para iniciar sesión no sirve para autorizar un pago aunque se redima contra otro
/// endpoint. Para step-up el propósito incluye la operación, así que un código emitido para
/// liberar un lote de payout tampoco vale para borrar un usuario.
/// </summary>
public interface IChallengeTokenService
{
    /// <summary>Vigencia de un challenge recién emitido.</summary>
    TimeSpan ChallengeLifetime { get; }

    /// <summary>
    /// Antigüedad máxima de un challenge todavía aceptado para reenviar el código: la firma se
    /// sigue verificando, pero la vigencia se mide contra esta ventana en vez de contra
    /// <see cref="ChallengeLifetime"/>. Así, quien tiene un código vencido puede pedir otro sin
    /// volver a escribir su contraseña.
    /// </summary>
    TimeSpan ResendGraceWindow { get; }

    /// <summary>Genera un código numérico de 6 dígitos, criptográficamente aleatorio.</summary>
    string GenerateCode();

    /// <summary>SHA-256 del código, en base64.</summary>
    string HashCode(string code);

    /// <summary>
    /// Emite un challenge para el usuario, el propósito y el canal dados.
    /// </summary>
    /// <param name="codeHash">
    /// Hash del código que se envió. Obligatorio para <see cref="TwoFactorChannel.Email"/> y
    /// <see cref="TwoFactorChannel.Sms"/>; se ignora para <see cref="TwoFactorChannel.Authenticator"/>,
    /// donde el código lo genera la aplicación del usuario y lo verifica Identity.
    /// </param>
    /// <param name="operationKey">
    /// Operación a autorizar. Obligatorio cuando <paramref name="purpose"/> es
    /// <see cref="TwoFactorPurpose.StepUp"/>; ignorado en el resto.
    /// </param>
    string Issue(
        string           userId,
        string           email,
        TwoFactorPurpose purpose,
        TwoFactorChannel channel,
        string?          codeHash,
        string?          operationKey = null);

    /// <summary>
    /// Valida firma, vigencia y que el propósito y la operación coincidan con lo esperado.
    /// </summary>
    Result<ChallengeClaims> Validate(
        string           challengeToken,
        TwoFactorPurpose expectedPurpose,
        string?          expectedOperationKey = null,
        bool             allowExpired = false);
}
