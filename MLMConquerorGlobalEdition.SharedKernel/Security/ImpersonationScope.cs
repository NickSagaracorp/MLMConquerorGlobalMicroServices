using System.Security.Claims;

namespace MLMConquerorGlobalEdition.SharedKernel.Security;

/// <summary>
/// QUÉ PUEDE HACER UN TOKEN DE SUPLANTACIÓN, según el propio token y no según lo que la interfaz
/// decida honrar.
/// </summary>
/// <remarks>
/// EL AGUJERO QUE CIERRA. <c>StartImpersonationHandler</c> calculaba <c>isReadOnly</c> para el
/// <c>SupportManager</c> sin <c>Admin</c> ni <c>SuperAdmin</c>, y lo devolvía en el CUERPO de la
/// respuesta. Al token no llegaba nada: se emitían dos horas con los roles completos del miembro
/// suplantado. El "solo lectura" era un dato informativo que la interfaz podía honrar o ignorar, y
/// quien pegara ese token en curl no estaba limitado por absolutamente nada. Podía cambiarle la
/// contraseña al miembro, mover su colocación en el árbol, transferir sus tokens o pedir un cobro.
///
/// QUÉ SIGNIFICA "SOLO LECTURA" EN TÉRMINOS DE AUTORIZACIÓN, que es la pregunta de verdad. No es
/// una lista de rutas —una lista se queda obsoleta el día que alguien añade la ruta 244— sino el
/// MÉTODO HTTP: GET, HEAD y OPTIONS leen; POST, PUT, PATCH y DELETE escriben. Esa frontera ya está
/// dibujada en el diseño de la API, la respeta cada ruta que existe hoy y, sobre todo, la respeta
/// SOLA cualquier ruta que se añada mañana. Es la única definición de "solo lectura" que no hay que
/// mantener.
///
/// FALLA CERRADO. Un método que no sea de lectura está prohibido salvo que la ruta lleve
/// explícitamente <see cref="ReadOnlySafeAttribute"/>. Las excepciones son un permiso que se
/// concede una a una y se ve en el código de la ruta, no un olvido que se cuela.
///
/// POR QUÉ ESTÁ EN SharedKernel Y NO EN AdminAPI. El token lo EMITE AdminAPI, pero se USA contra
/// BizCenter, RankEngine, TicketManagementSystem, Billing, CommissionEngine y SignupAPI: son esos
/// servicios los que tienen que negarse, no el que firma. Aquí lo alcanzan los siete. La parte que
/// necesita alojamiento web —el middleware que mira el método y el endpoint— vive en
/// SharedKernel.Server, que es la mitad de servidor de este mismo proyecto.
///
/// LO QUE ESTO NO ES. No sustituye a la comprobación de propiedad ni a los roles: un token de
/// suplantación de solo lectura sigue viendo todo lo que el miembro suplantado ve. Lo que ya no
/// puede es cambiar nada.
/// </remarks>
public static class ImpersonationScope
{
    /// <summary>
    /// El claim que marca la restricción. Lo escribe <c>JwtService.GenerateAccessToken</c> y solo
    /// aparece en tokens de suplantación restringidos; su ausencia no significa "sin restricción
    /// conocida" sino "no es un token restringido", que es lo mismo para todos los demás tokens.
    /// </summary>
    public const string ReadOnlyClaim = "impersonationReadOnly";

    /// <summary>
    /// El único valor que cuenta como restricción activa. Se compara exacto y en minúscula para
    /// que un <c>"True"</c> escrito a mano en otro sitio no pase por alto la restricción por una
    /// diferencia de mayúsculas: si no dice <c>true</c>, no está restringido, y si no está
    /// restringido es porque quien lo emitió no lo restringió.
    /// </summary>
    public const string ReadOnlyValue = "true";

    /// <summary>Métodos que solo leen. Todo lo demás escribe.</summary>
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS"];

    /// <summary>¿Este token viaja restringido a lectura?</summary>
    public static bool IsReadOnly(ClaimsPrincipal? user) =>
        user?.FindFirst(ReadOnlyClaim)?.Value is { } value &&
        string.Equals(value, ReadOnlyValue, StringComparison.Ordinal);

    /// <summary>¿Este método HTTP solo lee?</summary>
    public static bool IsSafeMethod(string? method) =>
        method is not null &&
        SafeMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
}
