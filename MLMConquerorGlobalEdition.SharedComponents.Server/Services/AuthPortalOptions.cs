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
    //  El contrato con la pantalla de segundo factor de este portal
    //
    //  Las dos propiedades de aquí abajo son temporales y traen los valores buenos por defecto: las
    //  necesita SOLO el centro de negocios, cuya pantalla /two-factor es anterior al componente
    //  compartido TwoFactorVerify. En cuanto monte ese componente —como ya hizo administración—
    //  dejará de declararlas y las dos propiedades se pueden borrar de aquí.
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Cómo se llama, en la URL de la pantalla de segundo factor, el parámetro que lleva el destino
    /// ya enmascarado (<c>n****@@dominio.com</c>, <c>***4321</c>).
    /// </summary>
    /// <remarks>
    /// El componente compartido lo lee de <c>target</c>; la pantalla propia del centro de negocios
    /// lo lee de <c>email</c>. El VALOR es el mismo en los dos casos —el destino enmascarado que
    /// devuelve la API—, así que esto es de verdad solo el nombre.
    /// </remarks>
    public string TwoFactorTargetQueryParam { get; init; } = "target";

    /// <summary>
    /// Código único que se pone en la URL cuando la pantalla de segundo factor tiene que enseñar un
    /// fallo, en vez del código que devolvió la API. Null —lo normal— propaga el de la API.
    /// </summary>
    /// <remarks>
    /// La pantalla de administración usa <c>TwoFactorVerify</c>, que sabe traducir el vocabulario
    /// entero de la API (<c>CODE_INVALID</c>, <c>TOO_MANY_REQUESTS</c>, <c>CHANNEL_UNAVAILABLE</c>…).
    /// La del centro de negocios solo compara con el literal <c>invalid_code</c>: propagarle el
    /// código de la API la dejaría sin enseñar NADA justo cuando el usuario teclea mal el código,
    /// que es el único momento en que esa pantalla tiene algo que decir.
    /// </remarks>
    public string? TwoFactorErrorCode { get; init; }
}
