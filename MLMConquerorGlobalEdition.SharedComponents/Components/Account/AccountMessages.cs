using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account;

/// <summary>
/// Traduce los códigos de error de recuperación de contraseña y confirmación de correo a
/// claves de <c>SharedResources</c>, y reconoce los estados de <see cref="ConfirmEmail"/>.
///
/// Aparte de <see cref="TwoFactorMessages"/> porque cubre un dominio distinto: aquella es del
/// segundo factor de un login ya identificado, esta es de flujos donde todavía no hay sesión
/// (recuperar contraseña, confirmar correo, cuenta bloqueada) y donde el riesgo de enumeración
/// de cuentas pesa en cada decisión de texto. Mezclarlas en un solo archivo habría hecho más
/// difícil ver qué código pertenece a qué flujo.
/// </summary>
public static class AccountMessages
{
    /// <summary>Clave de recurso para <paramref name="errorCode"/>; genérica si no se reconoce.</summary>
    /// <remarks>
    /// <c>USER_NOT_FOUND</c> solo debe usarse donde ya no hay riesgo de enumeración —por
    /// ejemplo, al fallar un reset con un token que ya demuestra que el enlace llegó por
    /// correo— nunca en el formulario de "olvidé mi contraseña", que no debe confirmar ni
    /// desmentir la existencia de una cuenta.
    /// </remarks>
    public static string ErrorKeyFor(string? errorCode) =>
        errorCode?.Trim().ToUpperInvariant() switch
        {
            "INVALID_TOKEN"          => "Account.Error.InvalidToken",
            "TOKEN_EXPIRED"          => "Account.Error.TokenExpired",
            "PASSWORD_RESET_FAILED"  => "Account.Error.PasswordResetFailed",
            "USER_NOT_FOUND"         => "Account.Error.UserNotFound",

            // SignupAPI no respondió. Lo emite AuthApiGateway y hasta ahora caía en la rama
            // genérica ("algo salió mal"), que le dice al usuario que reintente sin decirle que
            // el problema no es suyo. Es el mismo agujero que LoginErrorMessages tapó en las
            // pantallas de login, y por el mismo motivo: un código que emite el servidor no rompe
            // nada que el compilador pueda ver.
            //
            // La clave se llama ForgotPassword.ServerError por dónde nació, pero su texto no
            // menciona ninguna pantalla —"no se pudo contactar con el servidor"— y sirve igual en
            // el restablecimiento. Renombrarla obligaría a tocar los nueve .resx para no cambiar
            // ni una palabra de lo que lee el usuario.
            AuthApiGateway.Unreachable => "ForgotPassword.ServerError",

            // ChangePassword / SetPassword / teléfono (gestión de cuenta autenticada).
            "PASSWORD_CHANGE_FAILED" => "Account.Error.PasswordChangeFailed",
            // Mismo texto que PASSWORD_RESET_FAILED a propósito: en ambos casos la contraseña
            // nueva no cumple la política, y el usuario la tiene justo debajo en la lista de
            // requisitos. Un texto propio no diría nada distinto.
            "PASSWORD_SET_FAILED"    => "Account.Error.PasswordResetFailed",
            "PASSWORD_ALREADY_SET"   => "Account.Error.PasswordAlreadySet",
            "INVALID_PHONE"          => "Account.Error.InvalidPhone",
            "PHONE_NOT_FOUND"        => "Account.Error.PhoneNotFound",

            // Segundo factor gestionado desde la cuenta: alta y verificación del teléfono, cambio
            // de canal preferido, re-enrolamiento. Los cuatro códigos del código de un solo uso
            // reutilizan el texto de TwoFactorMessages porque dicen exactamente lo mismo y el
            // usuario tiene delante el mismo campo de seis dígitos; duplicarlos con otra
            // redacción solo conseguiría que la misma situación se explicase de dos maneras.
            "CODE_INVALID"           => "TwoFactor.Error.CodeInvalid",
            "CODE_EXPIRED"           => "TwoFactor.Error.CodeExpired",
            "TOO_MANY_ATTEMPTS"      => "TwoFactor.Error.TooManyAttempts",
            "TOO_MANY_REQUESTS"      => "TwoFactor.Error.TooManyRequests",

            // Estos dos sí llevan texto propio. El de TwoFactorMessages termina en "vuelve a
            // iniciar sesión", que es la salida correcta durante un login pero no aquí: quien ve
            // esto ya tiene sesión y lo que se le rompió fue el alta del teléfono, así que la
            // salida es volver a empezarla.
            "INVALID_CHALLENGE"      => "Account.Error.InvalidChallenge",
            // Y aquí CHANNEL_UNAVAILABLE no significa "no pudimos enviar el código" sino "el
            // canal que elegiste no tiene destino en esta cuenta", que se arregla dándoselo.
            "CHANNEL_UNAVAILABLE"    => "Account.Error.ChannelUnavailable",

            _                        => "Account.Error.Generic"
        };

    /// <summary>Estados que puede traer <see cref="ConfirmEmail.Status"/>.</summary>
    public const string ConfirmEmailSuccess = "Success";

    /// <inheritdoc cref="ConfirmEmailSuccess"/>
    public const string ConfirmEmailExpired = "Expired";

    /// <inheritdoc cref="ConfirmEmailSuccess"/>
    public const string ConfirmEmailInvalid = "Invalid";

    /// <summary>¿La confirmación de correo tuvo éxito?</summary>
    public static bool IsConfirmEmailSuccess(string? status) =>
        string.Equals(status?.Trim(), ConfirmEmailSuccess, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ¿El enlace de confirmación caducó? Cualquier otro valor —incluido <c>Invalid</c> o algo
    /// que esta interfaz no previó— se trata como enlace inválido: es la rama por defecto en
    /// <see cref="ConfirmEmail"/>, igual que el resto de mapeos de esta clase.
    /// </summary>
    public static bool IsConfirmEmailExpired(string? status) =>
        string.Equals(status?.Trim(), ConfirmEmailExpired, StringComparison.OrdinalIgnoreCase);
}
