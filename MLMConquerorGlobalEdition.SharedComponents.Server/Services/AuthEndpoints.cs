using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Resources;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Portal;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Lo de ENTRAR al portal: login, segundo factor, enrolamiento forzado y salida. Todo ocurre cuando
/// todavía no hay sesión y termina firmando una.
///
/// Lo de GESTIONAR la cuenta ya con sesión —contraseña, teléfono, datos personales— vive en
/// <see cref="AccountEndpoints"/>. Son dos momentos distintos, y con los diez manejadores de aquello
/// aquí dentro este archivo pasaría de trescientas líneas a más de seiscientas.
/// </summary>
/// <remarks>
/// ESTO ESTABA DUPLICADO. Había un <c>AuthEndpoints</c> en cada portal —302 líneas en AdminWeb, 189
/// en BizCenterWeb— y no eran el mismo código con otros literales: habían divergido en cosas que
/// nadie decidió. Administración sabía de enrolamiento forzado y el centro de negocios no; una
/// envolvía en <c>try</c> la lectura del cuerpo no-JSON y la otra solo en un camino de tres; una
/// propagaba el código de error de la API y la otra lo aplastaba a dos literales. Ninguna de esas
/// diferencias era una decisión de producto: eran dos archivos que se separaron.
///
/// Lo que sí es de cada portal entra por <see cref="AuthPortalOptions"/> —destinos, roles admitidos,
/// idioma— y los nombres de las cookies de reto por <see cref="ChallengeCookieNames"/>, el mismo
/// juego que ya usa la superficie de cuenta compartida. Eso es justo lo que impide escribir un reto
/// con un nombre y buscarlo con otro.
///
/// LAS LLAMADAS A LA API VAN TODAS POR <see cref="AuthApiGateway"/>. No es cosmético: los
/// manejadores de antes hacían <c>PostAsJsonAsync</c> a pelo y sin <c>try</c>, así que con SignupAPI
/// caída el login devolvía un 500 en la cara del usuario mientras <c>/account/forgot-password</c>
/// —que ya iba por el gateway— respondía con una redirección y un <c>SERVICE_UNAVAILABLE</c>. El
/// login era la única puerta que no lo usaba, que es como decir que el único sitio donde eso
/// importa era el único sitio sin protección.
///
/// Las opciones de las cookies de reto están en <see cref="ChallengeCookies"/>, compartida también
/// con el alta de teléfono.
/// </remarks>
public static class AuthEndpoints
{
    /// <summary>Longitud del código de un solo uso que emiten todos los canales, TOTP incluido.</summary>
    private const int CodeLength = 6;

    /// <summary>
    /// Nombre del parámetro con el que el destino ya enmascarado viaja hasta la pantalla del
    /// segundo factor. Lo lee <c>TwoFactorVerify</c>, que es el componente que montan los dos
    /// portales, así que ya no entra por opciones: era una propiedad de
    /// <c>AuthPortalOptions</c> mientras el centro de negocios tenía su pantalla propia.
    /// </summary>
    private const string TargetQueryParam = "target";

    // Códigos que la interfaz ya sabe traducir. Se propagan como CÓDIGO y no como mensaje: el texto
    // que ve el usuario lo decide la pantalla, que es la que puede traducirlo.
    private const string InvalidCredentials = "invalid";
    private const string AccessDenied       = "access_denied";
    private const string SessionExpiredCode = SessionExpiry.ErrorCode;
    private const string CodeInvalid        = "CODE_INVALID";
    private const string ChannelUnavailable = "CHANNEL_UNAVAILABLE";

    /// <summary>
    /// Los tres códigos con los que la API dice que un reto ya no vale para nada: firma mala,
    /// caducado o con los intentos agotados. Los tres acaban igual —fuera la cookie y de vuelta al
    /// login—, porque en los tres el usuario tiene que empezar de cero.
    /// </summary>
    private static readonly string[] SpentChallengeCodes =
        ["INVALID_CHALLENGE", "CODE_EXPIRED", "TOO_MANY_ATTEMPTS"];

    /// <summary>
    /// Los dos nombres del claim de rol. El token de hoy trae el largo, pero mirar solo uno deja la
    /// comprobación a merced de cómo se serialice el token mañana.
    /// </summary>
    private static readonly string[] RoleClaimTypes =
        [ClaimTypes.Role, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];

    // ===========================================================================================
    //  Los manejadores
    // ===========================================================================================

    /// <summary>
    /// El POST del formulario de login. Valida las credenciales contra SignupAPI y o firma la
    /// sesión, o desvía al segundo factor o al enrolamiento cuando la API lo pide.
    /// </summary>
    public static async Task<IResult> LoginAsync(
        [FromForm] LoginForm?               form,
        AuthApiGateway                      api,
        HttpContext                         httpContext,
        [FromServices] AuthPortalOptions    portal,
        [FromServices] ChallengeCookieNames challengeCookies,
        [FromServices] PortalSessionTokens  sessionTokens,
        CancellationToken                   ct)
    {
        form ??= new();

        var (outcome, refreshToken) = await api.LoginForTokensAsync(
            form.Email ?? string.Empty, form.Password ?? string.Empty, ct);

        if (!outcome.Success || outcome.Data is null)
            return Failure(portal.LoginPage, LoginErrorOf(outcome.ErrorCode));

        var tokens = outcome.Data;

        // Ramificar ANTES de tocar el token: en estas dos ramas AccessToken viene vacío, y leerlo
        // hacía fallar CanReadToken, que devolvía "credenciales inválidas" con credenciales buenas.
        if (tokens.RequiresEnrollment)
        {
            if (string.IsNullOrWhiteSpace(tokens.EnrollmentToken))
                return Failure(portal.LoginPage, InvalidCredentials);

            ChallengeCookies.Set(httpContext, challengeCookies.Enrollment, tokens.EnrollmentToken!);
            return Results.Redirect(portal.EnrollAuthenticatorPage);
        }

        if (tokens.RequiresTwoFactor)
        {
            if (string.IsNullOrWhiteSpace(tokens.ChallengeToken))
                return Failure(portal.LoginPage, InvalidCredentials);

            ChallengeCookies.Set(httpContext, challengeCookies.Login, tokens.ChallengeToken!);
            return Results.Redirect(
                portal.TwoFactorPage + TargetQuery(tokens.MaskedTarget, '?'));
        }

        return await CompleteSignInAsync(
            httpContext, portal, sessionTokens, tokens.AccessToken, refreshToken);
    }

    /// <summary>
    /// Segundo paso del login: canjea el código de seis dígitos junto con el ChallengeToken que
    /// viaja en cookie. El código llega del formulario; el reto nunca sale de la cookie.
    /// </summary>
    public static async Task<IResult> LoginTwoFactorAsync(
        [FromForm] CodeForm?                form,
        AuthApiGateway                      api,
        HttpContext                         httpContext,
        [FromServices] AuthPortalOptions    portal,
        [FromServices] ChallengeCookieNames challengeCookies,
        [FromServices] PortalSessionTokens  sessionTokens,
        CancellationToken                   ct)
    {
        form ??= new();

        var challengeToken = ChallengeCookies.Read(httpContext, challengeCookies.Login);
        if (string.IsNullOrWhiteSpace(challengeToken))
            return SessionExpired(httpContext, portal, challengeCookies);

        // Un código con la forma equivocada no llega a salir: la API lo rechazaría igual, pero por
        // el camino le habría gastado al usuario uno de los intentos del reto.
        if (!IsWellFormedCode(form.Code))
            return Failure(portal.TwoFactorPage, CodeInvalid);

        var (outcome, refreshToken) = await api.VerifyTwoFactorForTokensAsync(
            challengeToken!, form.Code ?? string.Empty, ct);

        if (!outcome.Success || outcome.Data is null)
        {
            return ChallengeRejected(
                httpContext, portal, challengeCookies.Login,
                portal.TwoFactorPage, outcome.ErrorCodeOr(CodeInvalid));
        }

        ChallengeCookies.Delete(httpContext, challengeCookies.Login);
        return await CompleteSignInAsync(
            httpContext, portal, sessionTokens, outcome.Data.AccessToken, refreshToken);
    }

    /// <summary>
    /// Reenvía el código. La API emite un reto nuevo, así que la cookie se refresca con él; si no
    /// devuelve ninguno, la anterior sigue siendo la válida y se deja como está.
    /// </summary>
    public static async Task<IResult> ResendTwoFactorAsync(
        AuthApiGateway                      api,
        HttpContext                         httpContext,
        [FromServices] AuthPortalOptions    portal,
        [FromServices] ChallengeCookieNames challengeCookies,
        CancellationToken                   ct)
    {
        var challengeToken = ChallengeCookies.Read(httpContext, challengeCookies.Login);
        if (string.IsNullOrWhiteSpace(challengeToken))
            return SessionExpired(httpContext, portal, challengeCookies);

        var outcome = await api.ResendTwoFactorAsync(challengeToken!, ct);

        if (!outcome.Success || outcome.Data is null)
        {
            return ChallengeRejected(
                httpContext, portal, challengeCookies.Login,
                portal.TwoFactorPage, outcome.ErrorCodeOr(ChannelUnavailable));
        }

        if (!string.IsNullOrWhiteSpace(outcome.Data.ChallengeToken))
            ChallengeCookies.Set(httpContext, challengeCookies.Login, outcome.Data.ChallengeToken!);

        return Results.Redirect(
            $"{portal.TwoFactorPage}?resent=1{TargetQuery(outcome.Data.MaskedTarget, '&')}");
    }

    /// <summary>
    /// Cierra el enrolamiento con el primer código de la aplicación autenticadora y deja al usuario
    /// dentro: la API devuelve tokens reales al confirmar, no hace falta volver a iniciar sesión.
    /// </summary>
    public static async Task<IResult> EnrollAuthenticatorAsync(
        [FromForm] CodeForm?                form,
        AuthApiGateway                      api,
        HttpContext                         httpContext,
        [FromServices] AuthPortalOptions    portal,
        [FromServices] ChallengeCookieNames challengeCookies,
        [FromServices] PortalSessionTokens  sessionTokens,
        CancellationToken                   ct)
    {
        form ??= new();

        var enrollmentToken = ChallengeCookies.Read(httpContext, challengeCookies.Enrollment);
        if (string.IsNullOrWhiteSpace(enrollmentToken))
            return SessionExpired(httpContext, portal, challengeCookies);

        if (!IsWellFormedCode(form.Code))
            return Failure(portal.EnrollAuthenticatorPage, CodeInvalid);

        var (outcome, refreshToken) = await api.ConfirmEnrollmentForTokensAsync(
            enrollmentToken!, form.Code ?? string.Empty, ct);

        if (!outcome.Success || outcome.Data is null)
        {
            return ChallengeRejected(
                httpContext, portal, challengeCookies.Enrollment,
                portal.EnrollAuthenticatorPage, outcome.ErrorCodeOr(CodeInvalid));
        }

        ChallengeCookies.Delete(httpContext, challengeCookies.Enrollment);
        return await CompleteSignInAsync(
            httpContext, portal, sessionTokens, outcome.Data.AccessToken, refreshToken);
    }

    /// <summary>Cierra la sesión y se lleva por delante cualquier reto a medias.</summary>
    /// <param name="reason">
    /// Por qué se está saliendo, cuando no lo pidió el usuario. Hoy solo hay uno:
    /// <c>session_expired</c>, con el que llama <see cref="ApiAuthHandler"/> desde dentro del
    /// circuito. Es el único camino por el que una sesión caducada puede limpiar de verdad la cookie:
    /// allí la respuesta HTTP a mano es la del WebSocket y ya empezó, aquí hay una petición nueva.
    /// </param>
    /// <param name="returnUrl">
    /// A dónde volver después de cerrar, cuando quien llama no es una pantalla de este portal.
    /// </param>
    /// <remarks>
    /// El motivo se compara contra el único valor conocido y NO se propaga tal cual a la URL. Es
    /// deliberado: esto lo llama el navegador, así que el valor viene del usuario, y reflejarlo en la
    /// redirección convertiría la salida en un altavoz para meter texto ajeno en la pantalla de
    /// login.
    ///
    /// EL <c>returnUrl</c> ES LO QUE HACE POSIBLE EL REBOTE DEL ALTA. La aplicación de alta vive en
    /// otro origen; al cargarse manda el navegador aquí una sola vez para que la sesión que hubiera
    /// abierta en ese ordenador muera —el escenario del evento: la persona anterior se levantó sin
    /// salir—, y vuelve por aquí a donde iba, con el slug del patrocinador entero.
    ///
    /// Y POR ESO EL DESTINO SE VALIDA CONTRA UNA LISTA BLANCA, que es obligatorio y no opcional: sin
    /// ella esta ruta sería una redirección abierta que empieza en el dominio del portal y termina
    /// donde quiera quien escriba el enlace, justo en el instante en el que al usuario se le acaba de
    /// cerrar la sesión y espera que le vuelvan a pedir la contraseña. La lista es
    /// <see cref="AuthPortalOptions.SignOutReturnUrlAllowList"/> y falla cerrado: un destino que no
    /// esté en ella NO se sigue y el usuario acaba en la pantalla de login de este portal, que es lo
    /// que habría pasado sin <c>returnUrl</c> ninguno.
    ///
    /// EL ORDEN DE LOS DOS DESTINOS. El <c>returnUrl</c> válido gana al <c>reason</c>, y no chocan
    /// nunca: <c>reason</c> lo pone el propio circuito de este portal —que jamás pasa
    /// <c>returnUrl</c>— y <c>returnUrl</c> lo pone el rebote del alta, que jamás pasa
    /// <c>reason</c>.
    /// </remarks>
    public static async Task<IResult> LogoutAsync(
        HttpContext                         httpContext,
        AuthApiGateway                      api,
        [FromServices] AuthPortalOptions    portal,
        [FromServices] ChallengeCookieNames challengeCookies,
        [FromServices] PortalSessionTokens  sessionTokens,
        [FromQuery] string?                 reason = null,
        [FromQuery] string?                 returnUrl = null,
        CancellationToken                   ct = default)
    {
        // TODO lo que significa cerrar una sesión está en PortalSignOut: el refresh token en la
        // API, la entrada del almacén, las cookies de reto, la cookie de sesión y el principal de
        // esta petición. Lo comparte con el rebote de la aplicación de alta, que llega justo por
        // aquí, y por eso está fuera: dos listas de "lo que hay que limpiar" se desincronizan en
        // cuanto aparece la sexta cosa que limpiar.
        await PortalSignOut.KillAsync(httpContext, api, challengeCookies, sessionTokens, ct);

        // La sesión ya está muerta pase lo que pase con el destino. Eso importa: un returnUrl
        // rechazado tiene que costarle al visitante una vuelta al login, no dejarle la sesión viva.
        if (PortalSessionBounce.IsAllowedReturnUrl(returnUrl, portal.SignOutReturnUrlAllowList))
            return Results.Redirect(returnUrl!);

        return reason == SessionExpiredCode
            ? Failure(portal.LoginPage, SessionExpiredCode)
            : Results.Redirect(portal.LoginPage);
    }

    // ===========================================================================================
    //  Lo común
    // ===========================================================================================

    /// <summary>
    /// Único punto donde se construye el ClaimsPrincipal y se firma la sesión. Los tres caminos de
    /// entrada —login directo, verificación del segundo factor y confirmación del enrolamiento—
    /// pasan por aquí, así que la comprobación de rol se aplica siempre sobre el token FINAL y
    /// nunca sobre un reto.
    /// </summary>
    /// <param name="refreshToken">
    /// El refresh token que la API dejó en la cabecera <c>Set-Cookie</c> de la respuesta. Puede
    /// venir vacío —una API que no lo entregue, un camino que todavía no lo capture— y entonces la
    /// sesión funciona igual pero no se podrá renovar: cuando el JWT caduque acabará en el login,
    /// que es como acababa antes de que existiera la renovación.
    /// </param>
    /// <remarks>
    /// AQUÍ SE GUARDA EL REFRESH TOKEN, y este era el agujero. La API lo devolvía en cada inicio de
    /// sesión y este método lo DESCARTABA: sin él no había forma de renovar nada, así que caducar el
    /// JWT <em>era</em> que la sesión estaba muerta. Ahora entra como un claim más de la misma cookie
    /// —<c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Strict</c>— y además se siembra el almacén de
    /// sesión, que es lo que verán el circuito y el middleware.
    ///
    /// El identificador de sesión es nuevo en cada firma, también cuando el mismo usuario vuelve a
    /// entrar: dos inicios de sesión son dos sesiones, y compartir entrada haría que salir de una
    /// dejara a la otra con una credencial que ya no vale.
    /// </remarks>
    private static async Task<IResult> CompleteSignInAsync(
        HttpContext         httpContext,
        AuthPortalOptions   portal,
        PortalSessionTokens sessionTokens,
        string?             accessToken,
        string?             refreshToken)
    {
        var handler = new JwtSecurityTokenHandler();
        if (string.IsNullOrWhiteSpace(accessToken) || !handler.CanReadToken(accessToken))
            return Failure(portal.LoginPage, InvalidCredentials);

        var jwt    = handler.ReadJwtToken(accessToken);
        var claims = jwt.Claims.ToList();
        claims.Add(new Claim(SessionExpiry.AccessTokenClaim, accessToken!));

        if (!HasAdmittedRole(claims, portal.AllowedRoles))
            return Failure(portal.LoginPage, AccessDenied);

        // El nombre de ESTA sesión. No es una credencial —no abre nada— y por eso puede ir en la
        // cookie al lado de las que sí lo son.
        var sessionId = Guid.NewGuid().ToString("N");
        claims.Add(new Claim(SessionExpiry.SessionIdClaim, sessionId));

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            claims.Add(new Claim(SessionExpiry.RefreshTokenClaim, refreshToken!));
            sessionTokens.Seed(sessionId, new SessionTokens(accessToken!, refreshToken!));
        }

        // httpContext aquí ES la petición real del navegador, así que la cookie de sesión sale con
        // la respuesta de esta redirección.
        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        if (portal.FollowsMemberLanguage)
            ApplyMemberLanguage(httpContext, jwt);

        return Results.Redirect(portal.HomePage);
    }

    /// <summary>
    /// ¿Alguno de los roles del token está en la lista del portal? Sin lista, entra cualquier
    /// cuenta válida: es lo que hace el centro de negocios, que admite a todos sus miembros.
    /// </summary>
    private static bool HasAdmittedRole(
        IEnumerable<Claim> claims, IReadOnlyCollection<string>? allowedRoles)
    {
        if (allowedRoles is null || allowedRoles.Count == 0)
            return true;

        return claims
            .Where(c => RoleClaimTypes.Contains(c.Type))
            .Any(c => allowedRoles.Contains(c.Value));
    }

    /// <summary>
    /// Deja fijada la cookie de cultura con el idioma preferido del miembro, que viaja en el claim
    /// <c>default_language</c> del token. Así un primer inicio de sesión en un dispositivo nuevo ya
    /// aterriza en su idioma, sin pasar por la pantalla de perfil.
    /// </summary>
    private static void ApplyMemberLanguage(HttpContext httpContext, JwtSecurityToken jwt)
    {
        var appCode = jwt.Claims.FirstOrDefault(c => c.Type == "default_language")?.Value;
        if (string.IsNullOrWhiteSpace(appCode) || !LanguageCodeMapper.IsSupportedAppCode(appCode))
            return;

        var cultureName = LanguageCodeMapper.ToCultureName(appCode);

        httpContext.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureName, cultureName)),
            new CookieOptions
            {
                // UtcNow y no Now: es una fecha de protocolo HTTP, no un dato de negocio.
                Expires  = DateTimeOffset.UtcNow.AddYears(1),
                SameSite = SameSiteMode.Lax,
                Path     = "/"
            });
    }

    /// <summary>
    /// A dónde va el usuario cuando la API rechaza un reto.
    /// </summary>
    /// <remarks>
    /// Un reto gastado —firma mala, caducado o sin intentos— no se puede reintentar, así que fuera
    /// la cookie y de vuelta al login. Cualquier otro fallo deja el reto vivo y devuelve a la
    /// pantalla, donde el usuario puede volver a teclear o pedir otro código.
    /// </remarks>
    /// <remarks>
    /// El código de la API se propaga TAL CUAL a la pantalla. Antes había un parámetro para
    /// sustituirlo por uno propio del portal; existía solo para la pantalla vieja de segundo
    /// factor del centro de negocios, que únicamente sabía traducir el literal <c>invalid_code</c>.
    /// Ahora las dos pantallas montan <c>TwoFactorVerify</c>, que habla el vocabulario entero de la
    /// API, y esa sustitución solo serviría para tirar información por el camino.
    /// </remarks>
    private static IResult ChallengeRejected(
        HttpContext       httpContext,
        AuthPortalOptions portal,
        string            cookieName,
        string            screenPage,
        string            code)
    {
        if (SpentChallengeCodes.Contains(code))
        {
            ChallengeCookies.Delete(httpContext, cookieName);
            return Failure(portal.LoginPage, SessionExpiredCode);
        }

        return Failure(screenPage, code);
    }

    /// <summary>
    /// Sin cookie no hay nada que canjear: caducó o alguien entró directo a la URL. Vuelta al login
    /// con un código que esa pantalla ya sabe enseñar, y sin dejar atrás el otro reto.
    /// </summary>
    private static IResult SessionExpired(
        HttpContext httpContext, AuthPortalOptions portal, ChallengeCookieNames challengeCookies)
    {
        ChallengeCookies.Delete(httpContext, challengeCookies.Login);
        ChallengeCookies.Delete(httpContext, challengeCookies.Enrollment);
        return Failure(portal.LoginPage, SessionExpiredCode);
    }

    /// <summary>
    /// El código de error del login.
    /// </summary>
    /// <remarks>
    /// LA REGLA: se propaga lo que NO habla de las credenciales, y todo lo demás sale como
    /// <c>invalid</c>. La lista de lo propagable es
    /// <see cref="LoginErrorMessages.PropagatedFromLogin"/> y vive en la interfaz, que es la única
    /// que sabe qué códigos tiene traducidos; propagar uno que la pantalla no conozca la dejaría
    /// callada justo cuando tiene algo que decir.
    ///
    /// LO QUE SIGUE APLASTADO, y es deliberado: <c>INVALID_CREDENTIALS</c> y <c>ACCOUNT_LOCKED</c>
    /// salen los dos como <c>invalid</c>. Distinguirlos —igual que distinguir "ese correo no
    /// existe" de "esa contraseña no es"— convertiría el formulario en un oráculo con el que
    /// averiguar qué direcciones están registradas. Esa unificación no se toca.
    ///
    /// LO QUE ESTABA MAL: hasta ahora se propagaba SOLO <c>SERVICE_UNAVAILABLE</c> y el resto se
    /// aplastaba, y en ese "resto" entraban los dos códigos que la API devuelve DESPUÉS de dar la
    /// contraseña por buena. Con el emisor de códigos del segundo factor limitado
    /// (<c>TOO_MANY_REQUESTS</c>, tres por cuarto de hora) el usuario leía "credenciales
    /// inválidas" con credenciales correctas, y se ponía a revisar lo único que ya estaba bien.
    /// Ninguno de los dos revela si una cuenta existe: para llegar ahí ya hubo autenticación
    /// correcta sobre una cuenta con segundo factor activo.
    ///
    /// Un servicio que no responde tampoco es un fallo de credenciales. Antes ni siquiera llegaba
    /// hasta aquí: la llamada iba sin <c>try</c> y la excepción salía como un 500 en la cara del
    /// usuario.
    /// </remarks>
    private static string LoginErrorOf(string? gatewayErrorCode) =>
        !string.IsNullOrWhiteSpace(gatewayErrorCode) &&
        LoginErrorMessages.PropagatedFromLogin.Contains(gatewayErrorCode)
            ? gatewayErrorCode!
            : InvalidCredentials;

    /// <summary>
    /// Fragmento con el destino ya enmascarado para la pantalla del segundo factor, o cadena vacía
    /// si la API no devolvió ninguno (el autenticador no envía nada a ninguna parte).
    /// </summary>
    /// <remarks>
    /// Este sí va en la URL, al revés que el ChallengeToken: llega enmascarado por la API
    /// (<c>n****@@dominio.com</c>, <c>***4321</c>) y no es una credencial. Sin él, un usuario de SMS
    /// no tendría manera de ver a qué número se envió el código: el teléfono no viaja en el reto y
    /// la pantalla no tiene otra fuente.
    /// </remarks>
    private static string TargetQuery(string? maskedTarget, char separator) =>
        string.IsNullOrWhiteSpace(maskedTarget)
            ? string.Empty
            : $"{separator}{TargetQueryParam}={Uri.EscapeDataString(maskedTarget)}";

    /// <summary>Seis dígitos, ni uno más. Es lo que emiten los tres canales y también TOTP.</summary>
    private static bool IsWellFormedCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && code.Length == CodeLength && code.All(char.IsDigit);

    /// <summary>
    /// Vuelta a una pantalla con el código del fallo en la query. Un solo sitio para esto: el
    /// formato del parámetro lo leen las pantallas, y basta que un manejador lo escriba distinto
    /// para que ese error se pierda por el camino sin que nada falle a la vista.
    /// </summary>
    private static IResult Failure(string page, string errorCode)
    {
        var separator = page.Contains('?') ? '&' : '?';
        return Results.Redirect($"{page}{separator}error={Uri.EscapeDataString(errorCode)}");
    }

    // ===========================================================================================
    //  Formularios y contrato de la API
    // ===========================================================================================

    // TODOS los campos son opcionales y TODOS los manejadores hacen `form ??= new()`, por lo mismo
    // que en AccountEndpoints: cuando el cuerpo no trae NINGUNO de los campos del registro —un POST
    // vacío, un formulario recortado, alguien probando la ruta a mano— el enlazador de las minimal
    // API deja el parámetro en null, y el manejador reventaba con una NullReferenceException que
    // salía como 500. Lo que tiene que pasar es que el campo llegue vacío y la API lo rechace como
    // cualquier otra credencial mala.

    /// <summary>Los dos campos del formulario de login.</summary>
    public record LoginForm(string? Email = null, string? Password = null);

    /// <summary>El formulario solo aporta el código; el reto vive en la cookie.</summary>
    public record CodeForm(string? Code = null);

    // El registro con la forma de la respuesta ya no está aquí: es AuthTokensResult, en
    // ClientCore, el mismo que usan las MAUI. Tener una copia privada en este archivo era la
    // tercera de esa misma forma en la solución, y la anterior se quedó sin RequiresEnrollment sin
    // que nada se pusiera rojo.
}
