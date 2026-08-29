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

            // ChangePassword / SetPassword / teléfono (gestión de cuenta autenticada).
            "PASSWORD_CHANGE_FAILED" => "Account.Error.PasswordChangeFailed",
            // Mismo texto que PASSWORD_RESET_FAILED a propósito: en ambos casos la contraseña
            // nueva no cumple la política, y el usuario la tiene justo debajo en la lista de
            // requisitos. Un texto propio no diría nada distinto.
            "PASSWORD_SET_FAILED"    => "Account.Error.PasswordResetFailed",
            "PASSWORD_ALREADY_SET"   => "Account.Error.PasswordAlreadySet",
            "INVALID_PHONE"          => "Account.Error.InvalidPhone",

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
