namespace MLMConquerorGlobalEdition.ClientCore;

/// <summary>
/// Las llamadas de la PUERTA con nombre propio: entrar, resolver el segundo factor, cerrar el
/// enrolamiento, pedir el correo de recuperación y fijar la contraseña nueva.
/// </summary>
/// <remarks>
/// POR QUÉ EXISTE. La ruta <c>api/v1/auth/login</c> —y las demás— iba camino de estar escrita tres
/// veces: en las pantallas de los portales, en AdminApp y en BizCenterApp. Una cadena copiada no
/// falla al compilar cuando la API renombra su ruta; falla en caliente, en un cliente, y meses
/// después. Con la ruta en un solo sitio, un renombrado se hace una vez.
///
/// SON MÉTODOS DE EXTENSIÓN Y NO UN SERVICIO NUEVO A PROPÓSITO. <see cref="AuthApiGateway"/> ya está
/// registrado en los dos portales —lo registra <c>AddAuthSurface</c>— y en las MAUI por su propio
/// cableado. Un servicio nuevo habría obligado a añadir un <c>AddScoped</c> en cada uno de los
/// cuatro anfitriones, y a acordarse de hacerlo en el quinto.
///
/// LAS RUTAS DEL SEGUNDO FACTOR YA ESTÁN AQUÍ. Estaban escritas a mano dentro de
/// <c>AuthEndpoints</c>, con la nota de que se moverían el día que hubiera un segundo cliente que
/// las llamara de verdad. Ese día llegó con AdminApp: la aplicación de administración dejó de tener
/// su propio login en AdminAPI y ahora entra por esta misma puerta, segundo factor incluido.
///
/// LAS QUE EMITEN TOKENS VAN POR <c>...ForTokensAsync</c>. El refresh token NO viene en el cuerpo:
/// la API lo vacía a propósito y lo entrega en la cabecera <c>Set-Cookie</c>. Quien llame al login,
/// a la verificación del código o a la confirmación del enrolamiento con el método corriente se
/// queda con la sesión a medias —token de acceso sí, con qué renovarlo no— y no se entera hasta que
/// caduca. Por eso esos tres NO tienen variante corriente.
///
/// AQUÍ NO SE DECIDE NADA DE INTERFAZ. Se devuelve el <see cref="ApiOutcome{T}"/> tal cual lo da el
/// gateway, con su código de error, porque quién enseña qué es cosa de la pantalla —que es la que
/// puede traducirlo— y porque el mismo fallo se cuenta distinto en un portal y en el otro.
/// </remarks>
public static class AuthApiGatewayExtensions
{
    // -----------------------------------------------------------------------------------------
    //  Las rutas. Públicas porque son el contrato con SignupAPI y hay pruebas que las nombran.
    // -----------------------------------------------------------------------------------------

    /// <summary>Validación de credenciales. Anónima: es justo lo que ocurre antes de haber sesión.</summary>
    public const string LoginPath = "api/v1/auth/login";

    /// <summary>Canje del código de seis dígitos junto con el reto que emitió el login.</summary>
    public const string TwoFactorVerifyPath = "api/v1/auth/two-factor/verify";

    /// <summary>Reenvío del código. Devuelve un reto NUEVO que sustituye al anterior.</summary>
    public const string TwoFactorResendPath = "api/v1/auth/two-factor/resend";

    /// <summary>Apertura del enrolamiento TOTP: clave compartida, URI <c>otpauth://</c> y su QR.</summary>
    public const string TwoFactorEnrollBeginPath = "api/v1/auth/two-factor/enroll/begin";

    /// <summary>Cierre del enrolamiento con el primer código de la aplicación autenticadora.</summary>
    public const string TwoFactorEnrollConfirmPath = "api/v1/auth/two-factor/enroll/confirm";

    /// <summary>Petición del correo de recuperación.</summary>
    public const string ForgotPasswordPath = "api/v1/auth/forgot-password";

    /// <summary>Cambio de contraseña con el token del correo.</summary>
    public const string ResetPasswordPath = "api/v1/auth/reset-password";

    // -----------------------------------------------------------------------------------------
    //  Las llamadas que EMITEN TOKENS. Todas devuelven además el refresh token del Set-Cookie.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Entra con correo y contraseña. La respuesta puede no traer sesión todavía: si la cuenta
    /// tiene segundo factor, o si su rol lo exige y aún no lo ha configurado, lo que vuelve es un
    /// reto —ver <see cref="AuthTokensResult"/>—, y quien llama tiene que llevar al usuario a la
    /// pantalla que corresponda en vez de dar por hecho que ya está dentro.
    /// </summary>
    public static Task<(ApiOutcome<AuthTokensResult> Outcome, string? RefreshToken)> LoginForTokensAsync(
        this AuthApiGateway api,
        string              email,
        string              password,
        CancellationToken   ct = default) =>
        api.CallForTokensAsync<AuthTokensResult>(
            HttpMethod.Post, LoginPath,
            new { Email = email, Password = password }, ct);

    /// <summary>
    /// Canjea el código de seis dígitos y el reto por los tokens de verdad.
    /// </summary>
    /// <remarks>
    /// El reto NO lo escribe el usuario: lo guarda quien llama —cookie HttpOnly en los portales,
    /// almacenamiento seguro en las MAUI— desde que lo emitió el login. Lo único que aporta la
    /// pantalla es el código.
    /// </remarks>
    public static Task<(ApiOutcome<AuthTokensResult> Outcome, string? RefreshToken)> VerifyTwoFactorForTokensAsync(
        this AuthApiGateway api,
        string              challengeToken,
        string              code,
        CancellationToken   ct = default) =>
        api.CallForTokensAsync<AuthTokensResult>(
            HttpMethod.Post, TwoFactorVerifyPath,
            new { ChallengeToken = challengeToken, Code = code }, ct);

    /// <summary>
    /// Cierra el enrolamiento con el primer código de la aplicación autenticadora. La API deja al
    /// usuario dentro sin pedirle que vuelva a iniciar sesión, así que esto también emite tokens.
    /// </summary>
    public static Task<(ApiOutcome<AuthTokensResult> Outcome, string? RefreshToken)> ConfirmEnrollmentForTokensAsync(
        this AuthApiGateway api,
        string              enrollmentToken,
        string              code,
        CancellationToken   ct = default) =>
        api.CallForTokensAsync<AuthTokensResult>(
            HttpMethod.Post, TwoFactorEnrollConfirmPath,
            new { EnrollmentToken = enrollmentToken, Code = code }, ct);

    // -----------------------------------------------------------------------------------------
    //  Las que no emiten tokens
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Reenvía el código. La API emite un reto NUEVO: quien llama tiene que sustituir el que
    /// guardaba por el que vuelve aquí, o el siguiente canje irá con uno ya gastado.
    /// </summary>
    public static Task<ApiOutcome<AuthTokensResult>> ResendTwoFactorAsync(
        this AuthApiGateway api,
        string              challengeToken,
        CancellationToken   ct = default) =>
        api.CallAsync<AuthTokensResult>(
            HttpMethod.Post, TwoFactorResendPath,
            new { ChallengeToken = challengeToken },
            authenticated: false, ct);

    /// <summary>
    /// Pide el correo de recuperación.
    /// </summary>
    /// <remarks>
    /// La API responde 200 exista o no la cuenta, para que este formulario no sirva de oráculo para
    /// enumerar direcciones registradas. Quien llame tiene que respetar esa cautela y enseñar el
    /// mismo mensaje en los dos casos: un <c>Success</c> aquí NO significa que la cuenta exista.
    /// </remarks>
    public static Task<ApiOutcome> ForgotPasswordAsync(
        this AuthApiGateway api,
        string              email,
        CancellationToken   ct = default) =>
        api.CallAsync(
            HttpMethod.Post, ForgotPasswordPath,
            new { Email = email },
            authenticated: false, ct);

    /// <summary>
    /// Fija la contraseña nueva con el token del correo.
    /// </summary>
    /// <param name="userId">
    /// El identificador tal cual venía en el enlace. El enlace del correo lleva <c>userId</c> y no
    /// la dirección: un correo en la query se queda en el historial del navegador, en los registros
    /// de cualquier proxy intermedio y en la cabecera <c>Referer</c> hacia todo recurso externo que
    /// cargue la página. El DTO de SignupAPI todavía acepta también <c>Email</c> en ese campo, por
    /// los enlaces viejos que siguen circulando.
    /// </param>
    public static Task<ApiOutcome> ResetPasswordAsync(
        this AuthApiGateway api,
        string              userId,
        string              token,
        string              newPassword,
        CancellationToken   ct = default) =>
        api.CallAsync(
            HttpMethod.Post, ResetPasswordPath,
            new { UserId = userId, Token = token, NewPassword = newPassword },
            authenticated: false, ct);
}

/// <summary>
/// Lo que devuelven los endpoints de autenticación, recortado a lo que un cliente necesita mirar.
/// </summary>
/// <remarks>
/// Las tres respuestas posibles de <see cref="AuthApiGatewayExtensions.LoginForTokensAsync"/> se
/// distinguen por banderas y no por tipos distintos porque así llegan de la API. Hay que mirarlas EN
/// ESTE ORDEN —enrolamiento, segundo factor, sesión— y ramificar ANTES de tocar
/// <see cref="AccessToken"/>: en las dos primeras viene vacío, y leerlo como si fuera un JWT es lo
/// que hacía que unas credenciales buenas acabasen en "credenciales inválidas".
///
/// ES EL ÚNICO REGISTRO DE ESTA FORMA EN LA SOLUCIÓN. <c>AuthEndpoints</c> tenía su propia copia
/// privada, y antes de eso el centro de negocios tenía una tercera SIN
/// <see cref="RequiresEnrollment"/> ni <see cref="EnrollmentToken"/>: el día que un rol de miembro
/// entrara en <c>Auth:TwoFactor:MandatoryRoles</c>, aquel portal habría mandado al usuario a
/// <c>/login?error=invalid</c> sin explicación. Con un solo registro eso no puede volver a pasar en
/// un cliente y no en otro.
/// </remarks>
public sealed record AuthTokensResult
{
    /// <summary>El JWT de la sesión. Vacío mientras haya un reto por resolver.</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// EXISTE EN EL CONTRATO Y SIEMPRE LLEGA VACÍO. La API lo pone a cadena vacía antes de
    /// responder (<c>response.RefreshToken = string.Empty</c>) y entrega el token de verdad en la
    /// cabecera <c>Set-Cookie</c>. Se deja declarado para que quien lea este registro con el de la
    /// API delante vea que no falta nada, pero leerlo de aquí es quedarse sin refresco y no
    /// enterarse hasta el segundo refresco; el bueno lo traen los <c>...ForTokensAsync</c>.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>Caducidad del token. Del protocolo, así que va en UTC.</summary>
    public DateTime TokenExpiry { get; init; }

    /// <summary>La cuenta tiene segundo factor y hace falta el código de <see cref="ChallengeToken"/>.</summary>
    public bool RequiresTwoFactor { get; init; }

    /// <summary>El reto que se canjea junto con el código de seis dígitos.</summary>
    public string? ChallengeToken { get; init; }

    /// <summary>El rol de la cuenta exige segundo factor y todavía no lo tiene configurado.</summary>
    public bool RequiresEnrollment { get; init; }

    /// <summary>El reto con el que se abre y se cierra el alta de la aplicación autenticadora.</summary>
    public string? EnrollmentToken { get; init; }

    /// <summary>Canal del reto: <c>Email</c>, <c>Sms</c> o <c>Authenticator</c>.</summary>
    public string? Channel { get; init; }

    /// <summary>A dónde fue el código, ya enmascarado por la API. Vacío con el autenticador.</summary>
    public string? MaskedTarget { get; init; }
}
