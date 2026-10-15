using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Authn.Abstractions;

/// <summary>
/// Emite y valida el "challenge": un JWT de corta vida que lleva dentro una huella del código
/// de 6 dígitos que se envió al usuario. Es stateless — no hay tabla de códigos pendientes:
/// el código vuelve del cliente y se compara contra la huella que viaja firmada en el token.
///
/// La huella es un HMAC con clave del servidor, atado al <c>jti</c> del propio reto, y no un
/// hash sin clave. El JWT va firmado pero NO cifrado: quien tiene el reto (el que acaba de
/// escribir la contraseña, o quien robó un token de acceso y pidió un step-up) lee el payload
/// con un simple base64. Con un SHA-256 a secas, probar el millón de códigos posibles fuera de
/// línea lleva milisegundos y el límite de intentos no sirve de nada porque no se intenta
/// contra el servidor. Sin la clave la huella no dice nada del código, y atada al jti la de un
/// reto no vale para otro.
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

    /// <summary>
    /// Emite un challenge para el usuario, el propósito y el canal dados.
    /// </summary>
    /// <param name="code">
    /// El código en claro que se envió. Recibe el código y no una huella ya calculada porque la
    /// huella depende del <c>jti</c>, que nace aquí: así no hay forma de emitir un reto con una
    /// huella que no esté atada a él. Obligatorio para <see cref="TwoFactorChannel.Email"/> y
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
        string?          code,
        string?          operationKey = null);

    /// <summary>
    /// Si <paramref name="code"/> es el que se envió para este reto: recalcula la huella con el
    /// <c>jti</c> del reto presentado y la compara en tiempo constante. Falso para un reto de
    /// Authenticator, que no lleva huella (ese código lo verifica Identity).
    /// </summary>
    bool VerifyCode(ChallengeClaims claims, string code);

    /// <summary>
    /// Etiqueta con clave para la marca antirreplay de un código TOTP ya usado. Va con clave por
    /// la misma razón que la huella del reto: la clave de caché no puede llevar un hash sin clave
    /// de un código de 6 dígitos, que se invierte con un millón de intentos.
    /// </summary>
    string TotpReplayTag(string userId, string code);

    /// <summary>
    /// Valida firma, vigencia y que el propósito y la operación coincidan con lo esperado.
    /// </summary>
    Result<ChallengeClaims> Validate(
        string           challengeToken,
        TwoFactorPurpose expectedPurpose,
        string?          expectedOperationKey = null,
        bool             allowExpired = false);
}
