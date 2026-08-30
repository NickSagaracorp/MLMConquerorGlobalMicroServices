namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Lo que cambia de un portal a otro en el momento de ENTRAR: a dónde van sus redirecciones, quién
/// tiene permitido entrar y las dos rarezas que todavía tiene la pantalla de segundo factor del
/// centro de negocios.
///
/// Es a <see cref="AuthEndpoints"/> lo que <see cref="AccountPageRoutes"/> es a
/// <see cref="AccountEndpoints"/>, y existe por lo mismo: los dos portales tenían su propia copia
/// del archivo de login —302 líneas en administración, 189 en el centro de negocios— y las dos
/// copias ya habían divergido en cosas que nadie decidió, como que una arreglase el 500 de un
/// cuerpo no-JSON y la otra no, o que una supiera de enrolamiento obligatorio y la otra no.
///
/// Va aparte de <see cref="AccountPageRoutes"/> y no como ocho propiedades más suyas porque son dos
/// momentos distintos: aquello son las pantallas de un usuario que YA tiene sesión, esto es la
/// puerta. Un portal puede montar la puerta sin montar el área de cuenta —es exactamente lo que
/// hace hoy el centro de negocios— y juntarlas obligaría a declarar nueve rutas de pantallas que
/// ese portal todavía no tiene.
/// </summary>
/// <remarks>
/// Las RUTAS DE LOS ENDPOINTS (<c>/account/login</c>, <c>/account/login-2fa</c>…) no están aquí: las
/// pone cada <c>Program.cs</c> en sus <c>MapPost</c>, porque tienen que coincidir letra a letra con
/// el <c>action=</c> del formulario de cada pantalla y hoy no coinciden entre portales
/// (<c>/account/login-2fa</c> en administración, <c>/account/two-factor/verify</c> en el centro de
/// negocios). Meterlas aquí no las unificaría; solo movería de sitio la discrepancia.
/// </remarks>
public sealed record AuthPortalOptions
{
    // -------------------------------------------------------------------------------------------
    //  Destinos
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// La pantalla de login. Es a la vez el punto de partida y el destino de todo lo que sale mal
    /// antes de firmar la sesión, así que se escribe una sola vez.
    /// </summary>
    public required string LoginPage { get; init; }

    /// <summary>Pantalla donde se teclea el código del segundo factor.</summary>
    public required string TwoFactorPage { get; init; }

    /// <summary>
    /// Pantalla del alta de la aplicación autenticadora, a la que va el usuario cuando su rol exige
    /// segundo factor y todavía no lo tiene configurado.
    /// </summary>
    public required string EnrollAuthenticatorPage { get; init; }

    /// <summary>A dónde aterriza el usuario con la sesión ya firmada.</summary>
    public required string HomePage { get; init; }

    // -------------------------------------------------------------------------------------------
    //  Quién entra
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Los roles que este portal admite, o null para admitir a cualquier cuenta válida.
    /// </summary>
    /// <remarks>
    /// Administración pasa su lista de nueve roles de personal; el centro de negocios no pasa
    /// ninguna, porque admite a cualquier miembro. La comprobación se hace en un único sitio
    /// —sobre el token FINAL, nunca sobre un reto—, así que da igual por cuál de los tres caminos
    /// de entrada haya llegado el usuario.
    ///
    /// Null y lista vacía significan lo mismo a propósito: una lista vacía por descuido no puede
    /// convertirse en "no entra nadie" y dejar un portal cerrado sin que nada falle al compilar.
    /// </remarks>
    public IReadOnlyCollection<string>? AllowedRoles { get; init; }

    // -------------------------------------------------------------------------------------------
    //  El idioma del miembro
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Si al firmar la sesión se fija la cookie de cultura con el idioma preferido del miembro, que
    /// viaja en el claim <c>default_language</c> del token.
    /// </summary>
    /// <remarks>
    /// Lo hace el centro de negocios y no administración, y no es un descuido de este último: el
    /// claim solo lo emite SignupAPI cuando la cuenta tiene <c>MemberProfile</c>, así que para el
    /// personal interno no existe. Encenderlo en administración no cambiaría nada para la mayoría
    /// de su gente, pero sí para un administrador que además sea miembro, al que le voltearía el
    /// portal al idioma de su perfil sin haberlo pedido. Se queda apagado ahí, que es lo que hacía.
    /// </remarks>
    public bool FollowsMemberLanguage { get; init; }

    // -------------------------------------------------------------------------------------------
    //  Lo que ya NO hace falta declarar
    //
    //  Aquí vivían TwoFactorTargetQueryParam y TwoFactorErrorCode. Existían para sostener la
    //  pantalla /two-factor propia del centro de negocios, que leía el destino enmascarado del
    //  parámetro `email` en vez de `target` y solo sabía traducir el literal `invalid_code`. Desde
    //  que esa pantalla monta TwoFactorVerify —el mismo componente que administración— las dos
    //  cosas son iguales en los dos portales: el destino viaja siempre en `target` y el código de
    //  error de la API se propaga tal cual, porque el componente habla su vocabulario entero.
    //
    //  Se borran y no se dejan "por si acaso": una opción que nadie pasa es una rama de
    //  comportamiento que nadie ejecuta, y la siguiente persona que la lea tendrá que averiguar
    //  para quién era antes de poder tocar nada de lo que la rodea.
    // -------------------------------------------------------------------------------------------
}
