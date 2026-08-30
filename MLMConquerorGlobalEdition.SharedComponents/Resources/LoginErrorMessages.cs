using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.SharedComponents.Resources;

/// <summary>
/// El vocabulario que las pantallas de login entienden: qué códigos de <c>?error=</c> pueden llegar
/// a la URL y con qué mensaje traducido y con qué severidad se enseña cada uno.
/// </summary>
/// <remarks>
/// ESTO ESTABA REPARTIDO EN CADENAS DE <c>@if</c> DENTRO DE CADA PANTALLA, y por eso se rompía en
/// silencio. La puerta ya redirigía con <c>?error=SERVICE_UNAVAILABLE</c> cuando SignupAPI no
/// respondía —desde que el login pasó por <see cref="AuthApiGateway"/>— y NINGUNA de las dos
/// pantallas lo traducía: el usuario veía el formulario otra vez, sin un solo aviso, y volvía a
/// probar sus credenciales buenas contra un servicio caído. Un código nuevo en el servidor no rompe
/// nada que el compilador pueda ver, así que la única forma de que no vuelva a pasar es tener la
/// lista en un sitio y una prueba que la recorra.
///
/// Vive en SharedComponents y no en su mitad de servidor porque es una decisión de INTERFAZ —qué se
/// le enseña al usuario— y la comparten los dos portales web y, el día que monten su login, las dos
/// MAUI. Los códigos los emite <c>AuthEndpoints</c>, que está del lado servidor y toma de aquí sus
/// constantes: así el que escribe el código en la URL y el que lo traduce no pueden separarse.
/// </remarks>
public static class LoginErrorMessages
{
    // -------------------------------------------------------------------------------------------
    //  Los códigos
    // -------------------------------------------------------------------------------------------

    /// <summary>Credenciales que no valen. Nunca distingue entre correo y contraseña.</summary>
    public const string Invalid = "invalid";

    /// <summary>La cuenta es buena pero el portal no admite su rol.</summary>
    public const string AccessDenied = "access_denied";

    /// <summary>La cuenta existe y está desactivada.</summary>
    public const string Inactive = "inactive";

    /// <summary>La sesión caducó, o el reto del segundo factor ya no vale.</summary>
    public const string SessionExpired = "session_expired";

    /// <summary>
    /// SignupAPI no respondió. El código lo pone <see cref="AuthApiGateway"/> y se toma de allí para
    /// que no pueda cambiar en un sitio y no en el otro.
    /// </summary>
    public const string ServiceUnavailable = AuthApiGateway.Unreachable;

    /// <summary>
    /// La contraseña era buena, pero el emisor de códigos del segundo factor está limitado (tres
    /// por cuarto de hora) y no quiso emitir otro.
    /// </summary>
    /// <remarks>
    /// OBSERVADO EN CALIENTE, y es el motivo de que este código exista: con el limitador gastado,
    /// el login le decía al usuario "credenciales inválidas" mientras sus credenciales eran
    /// correctas — el fallo ocurre DESPUÉS de comprobar la contraseña, en
    /// <c>LoginHandler</c>, cuando <c>ITwoFactorService.IssueAsync</c> devuelve
    /// <c>TOO_MANY_REQUESTS</c>. El usuario ve un error que le manda a revisar lo único que ya
    /// estaba bien.
    ///
    /// Y NO ABRE NINGÚN ORÁCULO DE ENUMERACIÓN: para llegar a este punto hay que haber acertado la
    /// contraseña de una cuenta con segundo factor activo, así que verlo no dice nada de una
    /// cuenta que no se tenga ya. Eso lo distingue de <c>INVALID_CREDENTIALS</c> y
    /// <c>ACCOUNT_LOCKED</c>, que siguen saliendo los dos como <see cref="Invalid"/> a propósito.
    /// </remarks>
    public const string TooManyRequests = "TOO_MANY_REQUESTS";

    /// <summary>
    /// Misma familia que <see cref="TooManyRequests"/>: la contraseña era buena y el código no
    /// pudo salir, esta vez porque el canal preferido no tiene destino o su transporte falló.
    /// </summary>
    /// <remarks>
    /// Sale del mismo <c>if (!issued.IsSuccess)</c> de <c>LoginHandler</c> que el anterior, así
    /// que dejarlo aplastado a <see cref="Invalid"/> habría sido arreglar el fallo a medias: el
    /// mismo usuario con las mismas credenciales buenas seguiría leyendo que están mal, solo que
    /// por la otra rama.
    /// </remarks>
    public const string ChannelUnavailable = "CHANNEL_UNAVAILABLE";

    /// <summary>
    /// Los códigos que la puerta puede propagar tal cual porque NO hablan de las credenciales.
    /// </summary>
    /// <remarks>
    /// Es la lista que consulta <c>AuthEndpoints.LoginErrorOf</c>, y vive aquí y no allí por lo
    /// mismo que el resto de este archivo: quien decide qué se le puede contar al usuario es la
    /// interfaz, que es la única que sabe qué códigos tiene traducidos. Lo que no esté aquí sale
    /// como <see cref="Invalid"/>, que es el valor seguro.
    /// </remarks>
    public static readonly IReadOnlyCollection<string> PropagatedFromLogin =
        [ServiceUnavailable, TooManyRequests, ChannelUnavailable];

    // -------------------------------------------------------------------------------------------
    //  El mapa
    // -------------------------------------------------------------------------------------------

    /// <summary>Cómo se enseña un código: qué clave de recurso y con qué severidad.</summary>
    /// <param name="ResourceKey">Clave de <c>SharedResources</c> con el texto para el usuario.</param>
    /// <param name="AlertClass">La clase de Bootstrap del aviso, según lo grave que sea.</param>
    public sealed record LoginError(string ResourceKey, string AlertClass);

    private static readonly IReadOnlyDictionary<string, LoginError> Map =
        new Dictionary<string, LoginError>(StringComparer.OrdinalIgnoreCase)
        {
            [Invalid]            = new("Login.ErrorInvalid",            "alert-danger"),
            [AccessDenied]       = new("Login.ErrorAccessDenied",       "alert-warning"),
            [Inactive]           = new("Login.ErrorInactive",           "alert-warning"),
            [SessionExpired]     = new("Login.ErrorSessionExpired",     "alert-info"),
            [ServiceUnavailable] = new("Login.ErrorServiceUnavailable", "alert-warning"),

            // alert-warning y no alert-danger en los dos: no hay nada que el usuario haya hecho mal.
            //
            // TOO_MANY_REQUESTS lleva texto PROPIO y no el de la pantalla del segundo factor
            // ("has pedido demasiados códigos"): en el login ese código también puede llegar de un
            // 429 del borde —AuthApiGateway traduce cualquier TooManyRequests a este código— y
            // entonces el usuario no había pedido ningún código. El texto de aquí sirve para los
            // dos casos y no le hace buscar algo que no existe.
            [TooManyRequests]    = new("Login.ErrorTooManyRequests",         "alert-warning"),

            // CHANNEL_UNAVAILABLE sí reutiliza el del segundo factor, y en los nueve idiomas: dice
            // exactamente lo que pasó —"no pudimos enviarte el código"— y aquí no puede venir de
            // ninguna otra parte. Es el mismo criterio con el que AccountMessages reutiliza estas
            // claves para la gestión de cuenta.
            [ChannelUnavailable] = new("TwoFactor.Error.ChannelUnavailable", "alert-warning"),
        };

    /// <summary>Todos los códigos que las pantallas de login saben enseñar.</summary>
    public static IReadOnlyCollection<string> AllCodes => (IReadOnlyCollection<string>)Map.Keys;

    /// <summary>
    /// Cómo enseñar este código, o null si no hay nada que enseñar (sin error en la URL, o un código
    /// que esta versión de la interfaz no conoce — mejor callar que enseñar un literal en crudo).
    /// </summary>
    public static LoginError? For(string? errorCode) =>
        string.IsNullOrWhiteSpace(errorCode) ? null
        : Map.TryGetValue(errorCode, out var error) ? error
        : null;
}
