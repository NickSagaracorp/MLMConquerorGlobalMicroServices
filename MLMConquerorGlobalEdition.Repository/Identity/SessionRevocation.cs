using Microsoft.AspNetCore.Identity;

namespace MLMConquerorGlobalEdition.Repository.Identity;

/// <summary>
/// QUÉ SIGNIFICA EXPULSAR A QUIEN YA ESTABA DENTRO, en un solo sitio y con una sola regla.
///
/// El refresh token de una cuenta vive en <see cref="ApplicationUser.RefreshToken"/> y dura treinta
/// días. Mientras siga ahí, quien lo tenga puede pedir tokens de acceso nuevos sin contraseña y sin
/// segundo factor. Por eso toda operación que cambie la POSTURA DE SEGURIDAD de la cuenta —el juego
/// de credenciales con las que se entra, o el material con el que se demuestran— tiene que pasar por
/// aquí: si no, la medida que se acaba de tomar no alcanza a la sesión que ya estaba abierta.
/// </summary>
/// <remarks>
/// DÓNDE ESTÁ LA LÍNEA, que es la pregunta de verdad y no "¿revoco o no?":
///
///   • SE REVOCA cuando cambia QUÉ hace falta para entrar o CON QUÉ se demuestra: contraseña nueva,
///     contraseña restablecida, primera contraseña, segundo factor activado, segundo factor
///     apagado, teléfono confirmado —el canal SMS pasa a existir— , teléfono dado de baja, y correo
///     cambiado, que es a la vez el identificador de la cuenta, el destino del enlace de
///     recuperación y el canal de 2FA que siempre está disponible.
///
///   • NO SE REVOCA cuando solo cambia POR DÓNDE llega el código entre factores que ya existían y ya
///     estaban confirmados: el canal preferido. Ahí no hay nada nuevo que demostrar ni nada que haya
///     dejado de valer, y revocar convertiría un cambio de preferencia en un cierre de sesión.
///     Tampoco al dar de alta un teléfono todavía SIN confirmar: ese número no es un factor hasta
///     que se redime su código, y hasta entonces no abre nada.
///
/// POR QUÉ ESTO ES UNA CLASE Y NO DOS LÍNEAS COPIADAS EN CADA MANEJADOR. Ya estaban copiadas en
/// cuatro —cambio, restablecimiento y alta de contraseña, y la salida— y faltaban en las cinco que
/// tocan el segundo factor y el correo. Dos líneas sueltas no se pueden buscar: nada relaciona la
/// una con la otra, y la prueba de que se olvidan es que se olvidaron. Con un nombre, la pregunta
/// "¿qué operaciones expulsan a quien ya estaba dentro?" se responde mirando quién llama aquí.
///
/// LO QUE ESTO NO ALCANZA: el token de ACCESO ya emitido, que es autofirmado y vive lo que diga
/// <c>Jwt:AccessTokenExpiryMinutes</c>. Revocar aquí cierra la renovación, así que la sesión muere
/// como mucho al caducar ese token. Para que muera en el acto hace falta además matarla en el
/// portal, y de eso se encarga <c>PortalSignOut.KillAsync</c> desde los manejadores del área de
/// cuenta.
/// </remarks>
public static class SessionRevocation
{
    /// <summary>
    /// Deja la cuenta sin refresh token vigente, EN MEMORIA. Quien llame tiene que persistir después
    /// —normalmente con el <c>UpdateAsync</c> que ya iba a hacer de todos modos—.
    /// </summary>
    /// <returns>
    /// Si había algo que revocar. Sirve para que una prueba pueda distinguir "no revocó" de "no
    /// había sesión que revocar", que sobre los campos ya nulos se ven igual.
    /// </returns>
    public static bool RevokeLiveSessions(this ApplicationUser user)
    {
        var habiaSesion = user.RefreshToken is not null || user.RefreshTokenExpiry is not null;

        user.RefreshToken       = null;
        user.RefreshTokenExpiry = null;

        return habiaSesion;
    }

    /// <summary>
    /// Lo mismo, y además lo guarda. Para los manejadores que no tienen ya un <c>UpdateAsync</c>
    /// propio detrás.
    /// </summary>
    public static Task<IdentityResult> RevokeLiveSessionsAsync(
        this UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        user.RevokeLiveSessions();
        return userManager.UpdateAsync(user);
    }
}
