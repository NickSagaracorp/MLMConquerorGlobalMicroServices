using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Los manejadores de los formularios del área de cuenta: recuperar la contraseña, confirmar el
/// correo y todo lo que un usuario ya identificado hace sobre su propia cuenta.
///
/// Aparte de <c>AuthEndpoints</c> porque son dos momentos distintos. Allí vive lo de ENTRAR
/// —login, segundo factor, enrolamiento y salida—, que ocurre cuando todavía no hay sesión y
/// termina firmando una; aquí vive lo de GESTIONAR la cuenta, que ocurre después y da por supuesta
/// esa sesión. Con los diez manejadores de aquí metidos allí, aquel archivo pasaba de trescientas
/// líneas a más de seiscientas y dejaba de poder leerse de una sentada.
///
/// Lo que se repetía —montar la llamada, ponerle el Bearer, desenvolver el sobre y traducir el
/// fallo a un código— no está copiado en cada manejador: vive en <see cref="AuthApiGateway"/>, y
/// lo de redirigir con ese código, en <see cref="Failure"/>. Cada manejador de aquí se queda con
/// lo único que es suyo: qué lee del formulario, a qué ruta de la API llama y a dónde va después.
///
/// Los manejadores siguen siendo estáticos y reciben sus dependencias por parámetro, que es como
/// las inyectan las minimal API. <see cref="AccountPageRoutes"/> y <see cref="ChallengeCookieNames"/>
/// entran por ahí como dos más: lo único que cambia de un portal a otro son las rutas de sus
/// pantallas y cómo llama a sus cookies de reto, y con eso el archivo entero sirve igual para
/// administración que para el centro de negocios.
/// </summary>
public static class AccountEndpoints
{
    // ---------------------------------------------------------------------------------------
    //  Anónimos — el usuario todavía no tiene sesión.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Pide el correo de recuperación. La API responde 200 exista o no la cuenta —para que este
    /// formulario no sirva de oráculo para enumerar direcciones registradas— y esta redirección
    /// respeta esa cautela: siempre acaba en la misma pantalla, que dice lo mismo en los dos casos.
    /// </summary>
    public static async Task<IResult> ForgotPasswordAsync(
        [FromForm] EmailForm?         form,
        AuthApiGateway                api,
        [FromServices] AccountPageRoutes routes,
        CancellationToken             ct)
    {
        form ??= new();

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/forgot-password",
            new { Email = form.Email ?? string.Empty }, authenticated: false, ct);

        // Solo se rompe la ambigüedad cuando el fallo NO habla de la cuenta: si la API no
        // respondió, callarlo dejaría al usuario esperando un correo que nadie llegó a pedir.
        return outcome.Success
            ? Results.Redirect(routes.ForgotPasswordSentPage)
            : Failure(routes.ForgotPasswordPage, outcome.ErrorCodeOr("PASSWORD_RESET_FAILED"));
    }

    /// <summary>
    /// Fija la contraseña nueva con el token del correo.
    /// </summary>
    /// <remarks>
    /// El identificador va en <c>UserId</c>, que es lo que trae el enlace del correo y lo que
    /// <c>ResetPassword.razor</c> postea. Antes se metía ese userId en el campo <c>Email</c>
    /// porque el DTO de SignupAPI no tenía otro sitio donde ponerlo; ahora acepta ambos y
    /// prefiere <c>UserId</c>, así que el valor viaja en su propio campo.
    ///
    /// El enlace lleva userId y no el correo a propósito: la dirección en la URL acabaría en el
    /// historial del navegador, en los registros del proxy y en la cabecera <c>Referer</c> hacia
    /// cualquier recurso externo que cargue la página. <c>Email</c> sigue en el DTO porque
    /// BizCenterWeb manda su enlace viejo con el correo.
    /// </remarks>
    public static async Task<IResult> ResetPasswordAsync(
        [FromForm] ResetPasswordForm? form,
        AuthApiGateway                api,
        [FromServices] AccountPageRoutes routes,
        CancellationToken             ct)
    {
        form ??= new();

        // El enlace se conserva en la vuelta: sin userId y token el formulario no se vuelve a
        // pintar, y el usuario tendría que ir a buscar el correo otra vez por haberse equivocado
        // al teclear la contraseña.
        var returnUrl = $"{routes.ResetPasswordPage}?userId={Uri.EscapeDataString(form.UserId ?? string.Empty)}" +
                        $"&token={Uri.EscapeDataString(form.Token ?? string.Empty)}";

        if (!PasswordsMatch(form.NewPassword, form.ConfirmPassword))
            return Failure(returnUrl, "PASSWORD_RESET_FAILED");

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/reset-password",
            new
            {
                UserId      = form.UserId ?? string.Empty,
                Token       = form.Token ?? string.Empty,
                NewPassword = form.NewPassword ?? string.Empty
            },
            authenticated: false, ct);

        return outcome.Success
            ? Results.Redirect(routes.ResetPasswordDonePage)
            : Failure(returnUrl, outcome.ErrorCodeOr("PASSWORD_RESET_FAILED"));
    }

    // ---------------------------------------------------------------------------------------
    //  De gestión — requieren sesión. El Bearer lo pone AuthApiGateway desde el claim
    //  access_token de la cookie; sin él la llamada ni sale y vuelve como SESSION_EXPIRED.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Reenvía el correo de confirmación de dirección al correo de la propia sesión.
    /// </summary>
    /// <remarks>
    /// La dirección sale del claim del token, nunca del formulario: el botón de ManageIndex no
    /// lleva campos precisamente para que nadie pueda usar esta ruta como un emisor de correos a
    /// direcciones ajenas. El endpoint de la API es anónimo, así que la llamada va sin Bearer;
    /// lo que exige sesión aquí es de dónde se saca el correo.
    /// </remarks>
    public static async Task<IResult> ResendConfirmationAsync(
        HttpContext                   httpContext,
        AuthApiGateway                api,
        [FromServices] AccountPageRoutes routes,
        CancellationToken             ct)
    {
        var email = EmailOfSession(httpContext);
        if (string.IsNullOrWhiteSpace(email))
            return Failure(routes.ProfilePage, AuthApiGateway.SessionExpired);

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/email/send-confirmation",
            new { Email = email }, authenticated: false, ct);

        return outcome.Success
            ? Results.Redirect($"{routes.ProfilePage}?resent=1")
            : Failure(routes.ProfilePage, outcome.ErrorCodeOr("SEND_CONFIRMATION_FAILED"));
    }

    /// <summary>Cambia la contraseña de una cuenta que ya tiene una.</summary>
    /// <remarks>
    /// SALE BIEN Y ACABA EN EL LOGIN. Ver <see cref="KillAndBackToLoginAsync"/>: la API acaba de
    /// invalidar el refresco de esta cuenta, así que esta sesión ya no se puede renovar.
    /// </remarks>
    public static async Task<IResult> ChangePasswordAsync(
        [FromForm] ChangePasswordForm? form,
        AuthApiGateway                 api,
        HttpContext                    httpContext,
        [FromServices] AccountPageRoutes routes,
        [FromServices] ChallengeCookieNames challengeCookies,
        [FromServices] PortalSessionTokens  sessionTokens,
        CancellationToken              ct)
    {
        form ??= new();

        if (!PasswordsMatch(form.NewPassword, form.ConfirmPassword))
            return Failure(routes.PasswordPage, "PASSWORD_CHANGE_FAILED");

        var outcome = await api.CallAsync(
            HttpMethod.Put, "api/v1/auth/change-password",
            new
            {
                CurrentPassword = form.CurrentPassword ?? string.Empty,
                NewPassword     = form.NewPassword ?? string.Empty
            },
            authenticated: true, ct);

        return outcome.Success
            ? await KillAndBackToLoginAsync(httpContext, api, challengeCookies, sessionTokens, routes, ct)
            : Failure(routes.PasswordPage, outcome.ErrorCodeOr("PASSWORD_CHANGE_FAILED"));
    }

    /// <summary>Fija la primera contraseña de una cuenta que no tiene ninguna.</summary>
    /// <remarks>Acaba en el login por lo mismo que <see cref="ChangePasswordAsync"/>.</remarks>
    public static async Task<IResult> SetPasswordAsync(
        [FromForm] SetPasswordForm?   form,
        AuthApiGateway                api,
        HttpContext                   httpContext,
        [FromServices] AccountPageRoutes routes,
        [FromServices] ChallengeCookieNames challengeCookies,
        [FromServices] PortalSessionTokens  sessionTokens,
        CancellationToken             ct)
    {
        form ??= new();

        if (!PasswordsMatch(form.NewPassword, form.ConfirmPassword))
            return Failure(routes.PasswordPage, "PASSWORD_SET_FAILED");

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/set-password",
            new { NewPassword = form.NewPassword ?? string.Empty }, authenticated: true, ct);

        return outcome.Success
            ? await KillAndBackToLoginAsync(httpContext, api, challengeCookies, sessionTokens, routes, ct)
            : Failure(routes.PasswordPage, outcome.ErrorCodeOr("PASSWORD_SET_FAILED"));
    }

    /// <summary>
    /// Da de alta el teléfono y manda el código que lo confirmará. El reto vuelve en cookie, no
    /// en la URL, por lo mismo que el del segundo factor del login.
    /// </summary>
    public static async Task<IResult> AddPhoneAsync(
        [FromForm] PhoneForm?         form,
        AuthApiGateway                api,
        HttpContext                   httpContext,
        [FromServices] AccountPageRoutes routes,
        [FromServices] ChallengeCookieNames challengeCookies,
        CancellationToken             ct)
    {
        form ??= new();

        var outcome = await api.CallAsync<PhoneChallenge>(
            HttpMethod.Post, "api/v1/auth/phone",
            new { PhoneE164 = form.PhoneE164 ?? string.Empty }, authenticated: true, ct);

        if (!outcome.Success || outcome.Data is null ||
            string.IsNullOrWhiteSpace(outcome.Data.ChallengeToken))
        {
            return Failure(routes.PhonePage, outcome.ErrorCodeOr("INVALID_PHONE"));
        }

        ChallengeCookies.Set(httpContext, challengeCookies.Phone, outcome.Data.ChallengeToken);

        // El destino sí va en la URL: llega ya enmascarado por la API (***4321) y no es una
        // credencial. Sin él, la pantalla de verificación no podría decir a qué número fue el
        // código, que es justo lo que permite darse cuenta de una cifra mal tecleada.
        var target = string.IsNullOrWhiteSpace(outcome.Data.MaskedTarget)
            ? string.Empty
            : $"?target={Uri.EscapeDataString(outcome.Data.MaskedTarget)}";

        return Results.Redirect($"{routes.PhoneVerifyPage}{target}");
    }

    /// <summary>Confirma el teléfono con el código del SMS y el reto que viaja en cookie.</summary>
    /// <remarks>
    /// Acaba en el login: aquí es donde el número se convierte en un factor de autenticación de la
    /// cuenta, y la API revoca el refresco por eso. Ver <see cref="KillAndBackToLoginAsync"/>.
    /// </remarks>
    public static async Task<IResult> VerifyPhoneAsync(
        [FromForm] CodeForm?          form,
        AuthApiGateway                api,
        HttpContext                   httpContext,
        [FromServices] AccountPageRoutes routes,
        [FromServices] ChallengeCookieNames challengeCookies,
        [FromServices] PortalSessionTokens  sessionTokens,
        CancellationToken             ct)
    {
        form ??= new();

        var challengeToken = ChallengeCookies.Read(httpContext, challengeCookies.Phone);
        if (string.IsNullOrWhiteSpace(challengeToken))
        {
            // Sin reto no hay nada que canjear: caducó o alguien entró directo a la URL. La
            // salida no es el login —el usuario sigue dentro— sino volver a empezar el alta.
            return Failure(routes.PhonePage, "INVALID_CHALLENGE");
        }

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/phone/verify",
            new { ChallengeToken = challengeToken, Code = form.Code ?? string.Empty },
            authenticated: true, ct);

        if (outcome.Success)
        {
            ChallengeCookies.Delete(httpContext, challengeCookies.Phone);
            return await KillAndBackToLoginAsync(
                httpContext, api, challengeCookies, sessionTokens, routes, ct);
        }

        // Un reto inválido o agotado ya no sirve para nada: fuera la cookie, y de vuelta al alta.
        var code = outcome.ErrorCodeOr("CODE_INVALID");
        if (code is "INVALID_CHALLENGE" or "CODE_EXPIRED" or "TOO_MANY_ATTEMPTS")
        {
            ChallengeCookies.Delete(httpContext, challengeCookies.Phone);
            return Failure(routes.PhonePage, code);
        }

        return Failure(routes.PhoneVerifyPage, code);
    }

    /// <summary>
    /// Da de baja el teléfono. Llega ya confirmado por el usuario: el enlace de ManageIndex no
    /// postea nada, solo repinta la pantalla con el aviso y es ese aviso el que trae este botón.
    /// </summary>
    public static async Task<IResult> RemovePhoneAsync(
        AuthApiGateway                api,
        HttpContext                   httpContext,
        [FromServices] AccountPageRoutes routes,
        [FromServices] ChallengeCookieNames challengeCookies,
        [FromServices] PortalSessionTokens  sessionTokens,
        CancellationToken             ct)
    {
        var outcome = await api.CallAsync(
            HttpMethod.Delete, "api/v1/auth/phone", body: null, authenticated: true, ct);

        // Un alta a medias con el teléfono ya borrado es un reto que apunta a un número que ya no
        // está en la cuenta.
        ChallengeCookies.Delete(httpContext, challengeCookies.Phone);

        // Retirar un factor revoca en la API, igual que confirmarlo. Ver KillAndBackToLoginAsync.
        return outcome.Success
            ? await KillAndBackToLoginAsync(httpContext, api, challengeCookies, sessionTokens, routes, ct)
            : Failure(routes.ProfilePage, outcome.ErrorCodeOr("PHONE_NOT_FOUND"));
    }

    /// <summary>
    /// Fija el canal por el que la cuenta recibirá su código de segundo factor.
    /// </summary>
    /// <remarks>
    /// El canal viaja como TEXTO —<c>Email</c>, <c>Sms</c>, <c>Authenticator</c>— porque es lo que
    /// manda el radio del formulario y lo que el enum <c>TwoFactorChannel</c> de la API acepta al
    /// deserializar. Aquí no se valida contra la lista de canales disponibles: eso lo hace el
    /// servidor, que es quien sabe si el canal tiene destino en esta cuenta, y lo rechaza con
    /// <c>CHANNEL_UNAVAILABLE</c>. Repetir la regla en el portal solo abriría la puerta a que las
    /// dos copias divergieran y se le ofreciese al usuario un canal por el que no le llegará nada.
    /// </remarks>
    public static async Task<IResult> SetTwoFactorChannelAsync(
        [FromForm] TwoFactorChannelForm? form,
        AuthApiGateway                api,
        [FromServices] AccountPageRoutes routes,
        CancellationToken             ct)
    {
        form ??= new();

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/two-factor/channel",
            new { Channel = form.Channel ?? string.Empty }, authenticated: true, ct);

        return outcome.Success
            ? Results.Redirect(routes.SecurityPage)
            : Failure(routes.SecurityPage, outcome.ErrorCodeOr("CHANNEL_UNAVAILABLE"));
    }

    /// <summary>
    /// Apaga el segundo factor de la cuenta.
    /// </summary>
    /// <remarks>
    /// Llega ya confirmado por el usuario: el enlace "desactivar" de <c>TwoFactorPanel</c> no
    /// postea nada, solo repinta la pantalla con el aviso, y es ese aviso el que trae este botón.
    /// Mismo patrón que "quitar teléfono" en <c>ManageIndex</c>.
    ///
    /// Que el rol del usuario lo tenga prohibido lo decide el SERVIDOR, con
    /// <c>TWO_FACTOR_REQUIRED</c>. El panel ya esconde el botón en ese caso, pero esconder un
    /// botón no cierra una ruta: quien la llame a mano se lleva el mismo rechazo.
    /// </remarks>
    public static async Task<IResult> DisableTwoFactorAsync(
        AuthApiGateway                api,
        HttpContext                   httpContext,
        [FromServices] AccountPageRoutes routes,
        [FromServices] ChallengeCookieNames challengeCookies,
        [FromServices] PortalSessionTokens  sessionTokens,
        CancellationToken             ct)
    {
        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/two-factor/disable",
            body: null, authenticated: true, ct);

        // Quitar el segundo factor cambia el juego de credenciales de la cuenta y la API revoca el
        // refresco. Ver KillAndBackToLoginAsync.
        return outcome.Success
            ? await KillAndBackToLoginAsync(httpContext, api, challengeCookies, sessionTokens, routes, ct)
            : Failure(routes.SecurityPage, outcome.ErrorCodeOr("TWO_FACTOR_DISABLE_FAILED"));
    }

    /// <summary>
    /// Sirve el archivo de datos personales al navegador.
    /// </summary>
    /// <remarks>
    /// Existe porque el enlace de descarga de <c>PersonalData.razor</c> es un <c>&lt;a href&gt;</c>
    /// normal y el endpoint de la API pide Bearer: el navegador no lo lleva, así que un enlace
    /// directo a la API devolvería 401. Esta ruta hace de intermediaria — pone el token de la
    /// sesión, y devuelve el archivo tal cual llega, con su nombre.
    /// </remarks>
    public static async Task<IResult> DownloadPersonalDataAsync(
        AuthApiGateway                api,
        IHttpClientFactory            httpClientFactory,
        ILoggerFactory                loggerFactory,
        [FromServices] AccountPageRoutes routes,
        CancellationToken             ct)
    {
        var token = await api.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return Failure(routes.ProfilePage, AuthApiGateway.SessionExpired);

        try
        {
            var httpClient = httpClientFactory.CreateClient(AuthApiGateway.HttpClientName);
            using var request = new HttpRequestMessage(
                HttpMethod.Get, "api/v1/auth/personal-data/download");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return Failure(routes.PersonalDataPage, "DOWNLOAD_FAILED");

            // Se materializa en memoria a propósito: el archivo son unos pocos kilobytes y así el
            // HttpResponseMessage puede cerrarse aquí en vez de vivir hasta que termine el
            // streaming, que es donde este patrón suele filtrar conexiones.
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);

            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                        ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                        ?? $"personal-data-{DateTime.UtcNow:yyyy-MM-dd}.json";

            return Results.File(bytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger(typeof(AccountEndpoints))
                .LogError(ex, "No se pudo descargar el archivo de datos personales.");
            return Failure(routes.PersonalDataPage, AuthApiGateway.Unreachable);
        }
    }

    // ---------------------------------------------------------------------------------------
    //  Lo común
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Mata esta sesión del portal entera y devuelve al usuario al login con el aviso puesto.
    /// </summary>
    /// <remarks>
    /// A DÓNDE LLEGAN LAS OPERACIONES QUE CAMBIAN LA POSTURA DE SEGURIDAD DE LA CUENTA: contraseña
    /// cambiada o fijada, teléfono confirmado o retirado, segundo factor apagado. Todas ellas
    /// REVOCAN el refresh token en la API (ver <c>SessionRevocation</c>, en Repository), y esa
    /// revocación por sí sola solo alcanza a la mitad de la sesión.
    ///
    /// POR QUÉ LA MITAD. La API deja de poder renovar, pero el token de ACCESO que el portal ya
    /// tiene en la mano es autofirmado y sigue valiendo hasta su <c>exp</c>. Sin esto, el usuario se
    /// queda dentro entre diez y quince minutos con una sesión que ya está muerta y se entera
    /// cuando el JWT caduca, a mitad de lo que estuviera haciendo, con un "sesión caducada" que no
    /// puede relacionar con el botón que pulsó. Y —esto es lo que importa— si quien pulsó el botón
    /// era el intruso, sigue dentro ese rato.
    ///
    /// SE REUSA <see cref="PortalSignOut.KillAsync"/> Y NO SE ESCRIBE UNA SEGUNDA LISTA. Una sesión
    /// del portal está en cuatro sitios —el refresco en la API, la entrada del almacén, las cookies
    /// de reto y la cookie de sesión más el principal de esta petición— y ese método es el único
    /// que los conoce todos. Copiar aquí "las tres cosas que hay que limpiar" es exactamente cómo se
    /// desincronizan las dos copias en cuanto aparezca la quinta.
    ///
    /// AQUÍ SÍ SE PUEDE, y no en cualquier sitio: estos manejadores atienden un POST de formulario
    /// del navegador, así que hay una respuesta HTTP que todavía no ha empezado y
    /// <c>SignOutAsync</c> puede escribir su cabecera. Dentro de un circuito de Blazor no la habría.
    ///
    /// EL CÓDIGO ES <c>session_expired</c> Y NO UNO NUEVO. Es exactamente lo que ha pasado —esta
    /// sesión dejó de valer— y las pantallas de login de los dos portales ya lo traducen en los
    /// nueve idiomas. Un código propio sería un literal sin traducir en ocho de ellos.
    /// </remarks>
    private static async Task<IResult> KillAndBackToLoginAsync(
        HttpContext          httpContext,
        AuthApiGateway       api,
        ChallengeCookieNames challengeCookies,
        PortalSessionTokens  sessionTokens,
        AccountPageRoutes    routes,
        CancellationToken    ct)
    {
        await PortalSignOut.KillAsync(httpContext, api, challengeCookies, sessionTokens, ct);
        return Failure(routes.LoginPage, SessionExpiry.ErrorCode);
    }

    /// <summary>
    /// Vuelta a la pantalla de origen con el código del fallo en la query. Un solo sitio para
    /// esto: el formato del parámetro lo leen los componentes de Account —todos por
    /// <c>ErrorCode</c>— y basta que un manejador lo escriba distinto para que ese error se
    /// pierda por el camino sin que nada falle a la vista.
    /// </summary>
    private static IResult Failure(string page, string errorCode)
    {
        var separator = page.Contains('?') ? '&' : '?';
        return Results.Redirect($"{page}{separator}error={Uri.EscapeDataString(errorCode)}");
    }

    /// <summary>
    /// La confirmación de contraseña se comprueba aquí porque la API no la recibe: sus DTOs solo
    /// llevan <c>NewPassword</c>. Sin esta comprobación, teclear mal la segunda casilla dejaría
    /// puesta la primera sin decir nada, y el usuario descubriría la contraseña que tiene de
    /// verdad en el siguiente inicio de sesión.
    /// </summary>
    private static bool PasswordsMatch(string? newPassword, string? confirmPassword) =>
        !string.IsNullOrEmpty(newPassword) &&
        string.Equals(newPassword, confirmPassword, StringComparison.Ordinal);

    /// <summary>
    /// El correo de la sesión. Sale del claim del token —donde lo dejó <c>CompleteSignInAsync</c>
    /// al copiar las claims del JWT— y se acepta tanto el nombre corto de JWT como el largo de
    /// .NET, igual que hace <c>AuthEndpoints</c> con los roles.
    /// </summary>
    private static string? EmailOfSession(HttpContext httpContext) =>
        httpContext.User.FindFirstValue("email")
        ?? httpContext.User.FindFirstValue(ClaimTypes.Email);

    // ---------------------------------------------------------------------------------------
    //  Formularios. Los nombres coinciden con los atributos name= de los componentes de
    //  SharedComponents.Components.Account: cambiar uno sin el otro deja el campo en null.
    // ---------------------------------------------------------------------------------------

    // TODOS los campos son opcionales y TODOS los manejadores hacen `form ??= new()`. No es
    // defensa por si acaso: cuando el cuerpo no trae NINGUNO de los campos del registro —un POST
    // vacío, un formulario recortado, alguien probando la ruta a mano— el enlazador de formularios
    // de las minimal API deja el parámetro en null, y el manejador reventaba con una
    // NullReferenceException que salía como 500. Lo que tiene que pasar es que el campo llegue
    // vacío y la validación lo rechace como cualquier otro valor malo.

    public record EmailForm(string? Email = null);

    /// <inheritdoc cref="ResetPasswordAsync"/>
    public record ResetPasswordForm(
        string? UserId = null, string? Token = null,
        string? NewPassword = null, string? ConfirmPassword = null);

    public record ChangePasswordForm(
        string? CurrentPassword = null, string? NewPassword = null, string? ConfirmPassword = null);

    public record SetPasswordForm(string? NewPassword = null, string? ConfirmPassword = null);

    public record PhoneForm(string? PhoneE164 = null);

    /// <summary>El formulario solo aporta el código; el reto vive en la cookie.</summary>
    public record CodeForm(string? Code = null);

    /// <summary>
    /// El canal preferido del segundo factor. El nombre <c>Channel</c> coincide con el
    /// <c>name=</c> de los radios de <c>TwoFactorPanel</c>; cambiar uno sin el otro deja el campo
    /// en null y el usuario se lleva un CHANNEL_UNAVAILABLE por un formulario vacío.
    /// </summary>
    public record TwoFactorChannelForm(string? Channel = null);

    /// <summary>Respuesta de <c>POST /api/v1/auth/phone</c>, recortada a lo que se usa.</summary>
    private sealed record PhoneChallenge
    {
        public string ChallengeToken { get; init; } = string.Empty;
        public string MaskedTarget   { get; init; } = string.Empty;
    }
}
