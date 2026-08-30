namespace MLMConquerorGlobalEdition.ClientCore;

/// <summary>
/// Las llamadas de la PUERTA con nombre propio: entrar, pedir el correo de recuperación y fijar la
/// contraseña nueva.
/// </summary>
/// <remarks>
/// POR QUÉ EXISTE. La ruta <c>api/v1/auth/login</c> —y las otras dos— iba camino de estar escrita
/// tres veces: en la pantalla de BizCenterWeb, en AdminApp y en BizCenterApp. Una cadena copiada no
/// falla al compilar cuando la API renombra su ruta; falla en caliente, en un cliente, y meses
/// después. Con la ruta en un solo sitio, un renombrado se hace una vez.
///
/// SON MÉTODOS DE EXTENSIÓN Y NO UN SERVICIO NUEVO A PROPÓSITO. <see cref="AuthApiGateway"/> ya está
/// registrado en los dos portales —lo registra <c>AddAuthSurface</c>— y en las MAUI lo estará por su
/// propio cableado. Un servicio nuevo habría obligado a añadir un <c>AddScoped</c> en cada uno de los
/// cuatro anfitriones, y a acordarse de hacerlo en el quinto. Una clase estática de extensiones no
/// necesita cableado ninguno: quien ya tiene el gateway, ya tiene esto.
///
/// AQUÍ NO SE DECIDE NADA DE INTERFAZ. Se devuelve el <see cref="ApiOutcome{T}"/> tal cual lo da el
/// gateway, con su código de error, porque quién enseña qué es cosa de la pantalla —que es la que
/// puede traducirlo— y porque el mismo fallo se cuenta distinto en un portal y en el otro.
///
/// LO QUE NO ESTÁ AQUÍ: las rutas que solo usa <c>AuthEndpoints</c> del lado servidor
/// (<c>two-factor/verify</c>, <c>two-factor/resend</c>, <c>two-factor/enroll/*</c>). Se quedan
/// escritas allí hasta que haya un segundo cliente que las llame de verdad; adelantarlas aquí sin
/// que aquel archivo las tome dejaría la ruta escrita en DOS sitios en vez de en uno, que es
/// exactamente lo que esta clase existe para evitar.
/// </remarks>
public static class AuthApiGatewayExtensions
{
    // -----------------------------------------------------------------------------------------
    //  Las rutas. Públicas porque son el contrato con SignupAPI y hay pruebas que las nombran.
    // -----------------------------------------------------------------------------------------

    /// <summary>Validación de credenciales. Anónima: es justo lo que ocurre antes de haber sesión.</summary>
    public const string LoginPath = "api/v1/auth/login";

    /// <summary>Petición del correo de recuperación.</summary>
    public const string ForgotPasswordPath = "api/v1/auth/forgot-password";

    /// <summary>Cambio de contraseña con el token del correo.</summary>
    public const string ResetPasswordPath = "api/v1/auth/reset-password";

    // -----------------------------------------------------------------------------------------
    //  Las llamadas
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Entra con correo y contraseña. La respuesta puede no traer sesión todavía: si la cuenta
    /// tiene segundo factor, o si su rol lo exige y aún no lo ha configurado, lo que vuelve es un
    /// reto —ver <see cref="AuthTokensResult"/>—, y quien llama tiene que llevar al usuario a la
    /// pantalla que corresponda en vez de dar por hecho que ya está dentro.
    /// </summary>
    public static Task<ApiOutcome<AuthTokensResult>> LoginAsync(
        this AuthApiGateway api,
        string              email,
        string              password,
        CancellationToken   ct = default) =>
        api.CallAsync<AuthTokensResult>(
            HttpMethod.Post, LoginPath,
            new { Email = email, Password = password },
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
/// Las tres respuestas posibles de <see cref="AuthApiGatewayExtensions.LoginAsync"/> se distinguen
/// por banderas y no por tipos distintos porque así llegan de la API. Hay que mirarlas EN ESTE
/// ORDEN —enrolamiento, segundo factor, sesión— y ramificar ANTES de tocar
/// <see cref="AccessToken"/>: en las dos primeras viene vacío, y leerlo como si fuera un JWT es lo
/// que hacía que unas credenciales buenas acabasen en "credenciales inválidas".
/// </remarks>
public sealed record AuthTokensResult
{
    /// <summary>El JWT de la sesión. Vacío mientras haya un reto por resolver.</summary>
    public string AccessToken { get; init; } = string.Empty;

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
