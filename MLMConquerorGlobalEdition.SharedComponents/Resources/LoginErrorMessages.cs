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
