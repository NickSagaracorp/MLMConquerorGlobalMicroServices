using System.Security.Claims;

namespace MLMConquerorGlobalEdition.SharedKernel.Configuration;

/// <summary>
/// Separa los tokens de reto de 2FA (login, enrolamiento y step-up) de los tokens de acceso,
/// para que un reto no autorice absolutamente nada.
///
/// El problema que resuelve: el reto se emite en el login <b>antes</b> de verificar ningún
/// código, y lleva <c>sub</c>. Mientras compartió emisor, audiencia y llave con los tokens de
/// acceso, nada lo distinguía para un middleware de bearer: cualquier endpoint <c>[Authorize]</c>
/// de cualquier servicio lo aceptaba como si el segundo factor ya se hubiera superado. Bastaban
/// el correo y la contraseña de la víctima para desactivarle el 2FA, cambiarle la contraseña o
/// exportar sus datos personales, sin tocar su correo, su SMS ni su autenticador.
///
/// El mecanismo es la <b>audiencia</b>, y se elige justo por dónde cae el fallo cuando alguien se
/// despista. Los nueve anfitriones ya validan la audiencia (<c>ValidateAudience = true</c> con
/// <c>ValidAudience = Jwt:Audience</c>), así que un reto emitido para <c>{audiencia}.challenge</c>
/// muere en la validación estándar del token <b>sin que ningún servicio tenga que acordarse de
/// comprobar nada</b>. Un servicio nuevo que se configure de la manera normal queda protegido el
/// día uno; la protección no depende de recordar añadir un guarda, que es exactamente lo que
/// falló aquí.
///
/// Alternativas descartadas: una llave de firma aparte protege igual pero obliga a distribuir y
/// rotar un par nuevo en todos los entornos, y el fallo de configuración deja de ser silencioso
/// para volverse una caída; comprobar el claim <c>purpose</c> en cada servicio depende de que los
/// nueve lo hagan y de que el décimo se acuerde — falla abierto. Ese claim se usa aquí solo como
/// segundo cinturón (<see cref="CarriesPurpose"/>), nunca como defensa única.
/// </summary>
public static class ChallengeAudience
{
    /// <summary>
    /// Sufijo que distingue la audiencia de los retos. Va pegado a la audiencia de acceso para
    /// que las dos salgan de una sola clave de configuración: si alguien cambia
    /// <c>Jwt:Audience</c>, las dos se mueven juntas y no pueden quedar desalineadas.
    /// </summary>
    public const string Suffix = ".challenge";

    /// <summary>
    /// Claim que marca el propósito del reto (<c>login</c>, <c>enrollment</c>,
    /// <c>step_up:{operación}</c>). Lo escribe el emisor de retos y ningún token de acceso lo
    /// lleva nunca.
    /// </summary>
    public const string PurposeClaim = "purpose";

    /// <summary>Audiencia de los retos derivada de la audiencia de acceso.</summary>
    public static string For(string accessAudience) => accessAudience + Suffix;

    /// <summary>
    /// Si el token trae el claim de propósito, es un reto y no debe autorizar nada.
    ///
    /// Es el segundo cinturón, no el primero: llega después de que la audiencia ya haya
    /// rechazado el reto, y existe para el caso en que alguien afloje la validación de audiencia
    /// en un servicio. Como única defensa sería frágil —depende de que cada anfitrión lo
    /// llame—; como refuerzo, cierra el hueco por partida doble.
    /// </summary>
    public static bool CarriesPurpose(IEnumerable<Claim> claims) =>
        claims.Any(c => string.Equals(c.Type, PurposeClaim, StringComparison.Ordinal));
}
