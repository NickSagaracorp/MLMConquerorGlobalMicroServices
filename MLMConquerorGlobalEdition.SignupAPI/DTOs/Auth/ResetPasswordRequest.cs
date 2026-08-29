namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Cambio de contraseña con el token del correo de recuperación.
/// </summary>
/// <remarks>
/// <b>Acepta dos identificadores y prefiere <see cref="UserId"/>.</b> El componente
/// <c>ResetPassword.razor</c> de SharedComponents postea <c>UserId</c>; la pantalla de
/// BizCenterWeb postea <c>Email</c>, que es lo único que este DTO sabía leer hasta ahora.
/// Aceptar los dos deja vivos a los dos clientes sin tener que cambiarlos a la vez.
///
/// El enlace del correo nuevo lleva <c>userId</c>: una dirección de correo en la query se queda
/// en el historial del navegador, en los registros de cualquier proxy intermedio y en la cabecera
/// <c>Referer</c> que la página manda a todo recurso externo que cargue. Un identificador opaco
/// no filtra nada de eso.
/// </remarks>
public class ResetPasswordRequest
{
    /// <summary>Identificador de la cuenta. Es el que trae el enlace del correo y el que gana si vienen los dos.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Dirección de la cuenta. Solo se usa si no viene <see cref="UserId"/>.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Token de Identity codificado en base64url. Viaja codificado porque el token crudo lleva
    /// '+', '/' y '=' — caracteres que una query string corrompe.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
