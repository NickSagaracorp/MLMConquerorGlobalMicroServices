using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.SharedKernel.Security;

/// <summary>
/// QUIÉN ES EL QUE LLAMA, según su token y no según lo que él mismo diga en la ruta o en el cuerpo.
///
/// Existe porque el patrón contrario estaba repartido por varias APIs: rutas
/// <c>/api/v1/members/{memberId}/…</c> y cuerpos con <c>MemberId</c> dentro, protegidos con un
/// <c>[Authorize]</c> pelado que solo comprueba que HAY sesión y nunca DE QUIÉN es. Con eso,
/// cualquier cuenta autenticada actúa sobre la cuenta de cualquier otra con solo cambiar una cadena.
/// </summary>
/// <remarks>
/// EL CLAIM SE LLAMA <c>memberId</c> Y NO <c>member_id</c>. Lo emite
/// <c>JwtService.GenerateAccessToken</c> con ese nombre exacto; el <c>CurrentUserService</c> de
/// SignupAPI leía el otro y por eso devolvía siempre cadena vacía. Una comprobación de propiedad
/// construida sobre un identificador que siempre viene vacío no protege nada —o lo cierra todo—, así
/// que el nombre vive aquí, en una constante, y no escrito a mano en cada sitio.
///
/// EL PERSONAL PASA POR ENCIMA. Las cuentas de <see cref="AppRoles.AdminRoles"/> no tienen
/// <c>MemberProfile</c> y por tanto no tienen <c>memberId</c>: obligarles a coincidir dejaría fuera a
/// todo el panel. Que un rol de personal pueda actuar sobre un miembro ya es la regla del sistema —es
/// lo que hacen todas las rutas <c>/api/v1/admin/…</c>—; lo que aquí se cierra es que lo pueda hacer
/// un miembro cualquiera.
///
/// FALLA CERRADO: sin token, sin claim o con un identificador vacío, la respuesta es que NO.
/// </remarks>
public static class CallerIdentity
{
    /// <summary>El claim con el identificador de miembro, tal y como lo escribe el emisor.</summary>
    public const string MemberIdClaim = "memberId";

    /// <summary>El nombre viejo del claim. Se lee por si queda algún token emitido antes.</summary>
    public const string LegacyMemberIdClaim = "member_id";

    /// <summary>
    /// El identificador de usuario del token. Se aceptan los dos nombres —el largo de .NET y el
    /// <c>sub</c> de JWT— por lo mismo que hace <c>AuthController</c>: cuál de los dos llega depende
    /// de si el mapeo de claims entrantes está activo, y mirar solo uno deja la comprobación a merced
    /// de esa configuración.
    /// </summary>
    public static string? UserIdOf(ClaimsPrincipal? user) =>
        NonEmpty(user?.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        ?? NonEmpty(user?.FindFirst("sub")?.Value);

    /// <summary>El identificador de miembro del token, o null si esta cuenta no es un miembro.</summary>
    public static string? MemberIdOf(ClaimsPrincipal? user) =>
        NonEmpty(user?.FindFirst(MemberIdClaim)?.Value)
        ?? NonEmpty(user?.FindFirst(LegacyMemberIdClaim)?.Value);

    /// <summary>¿Es una cuenta de personal interno?</summary>
    public static bool IsStaff(ClaimsPrincipal? user) =>
        user is not null && AppRoles.AdminRoles.Any(user.IsInRole);

    /// <summary>
    /// ¿Puede este token actuar sobre la cuenta de <paramref name="memberId"/>? Solo si es la suya, o
    /// si quien llama es personal.
    /// </summary>
    public static bool CanActOnMember(this ClaimsPrincipal? user, string? memberId)
    {
        if (string.IsNullOrWhiteSpace(memberId)) return false;
        if (IsStaff(user)) return true;

        var propio = MemberIdOf(user);
        return propio is not null &&
               string.Equals(propio, memberId, StringComparison.Ordinal);
    }

    private static string? NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
