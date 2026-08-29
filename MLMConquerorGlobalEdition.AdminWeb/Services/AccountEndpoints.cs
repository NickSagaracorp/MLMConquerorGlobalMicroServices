using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace MLMConquerorGlobalEdition.AdminWeb.Services;

/// <summary>
/// Los manejadores de los formularios del área de cuenta: recuperar la contraseña, confirmar el
/// correo y todo lo que un usuario ya identificado hace sobre su propia cuenta.
///
/// Aparte de <see cref="AuthEndpoints"/> porque son dos momentos distintos. Allí vive lo de
/// ENTRAR —login, segundo factor, enrolamiento y salida—, que ocurre cuando todavía no hay
/// sesión y termina firmando una; aquí vive lo de GESTIONAR la cuenta, que ocurre después y da
/// por supuesta esa sesión. Con los diez manejadores de aquí metidos allí, aquel archivo pasaba
/// de trescientas líneas a más de seiscientas y dejaba de poder leerse de una sentada.
///
/// Lo que se repetía —montar la llamada, ponerle el Bearer, desenvolver el sobre y traducir el
/// fallo a un código— no está copiado en cada manejador: vive en <see cref="AuthApiGateway"/>, y
/// lo de redirigir con ese código, en <see cref="Failure"/>. Cada manejador de aquí se queda con
/// lo único que es suyo: qué lee del formulario, a qué ruta de la API llama y a dónde va después.
/// </summary>
public static class AccountEndpoints
{
    // ---------------------------------------------------------------------------------------
    //  Rutas de las pantallas. Escritas una vez: son a la vez el destino del éxito y el del
    //  error, y una de las dos escrita a mano en otro sitio es una redirección que se queda atrás
    //  el día que la ruta cambie.
    // ---------------------------------------------------------------------------------------
    private const string ForgotPasswordPage     = "/admin/forgot-password";
    private const string ForgotPasswordSentPage = "/admin/forgot-password/sent";
    private const string ResetPasswordPage      = "/admin/reset-password";
    private const string ResetPasswordDonePage  = "/admin/reset-password/done";
    private const string ProfilePage            = "/admin/account";
    private const string PasswordPage           = "/admin/account/password";
    private const string PhonePage              = "/admin/account/phone";
    private const string PhoneVerifyPage        = "/admin/account/phone/verify";

    // ---------------------------------------------------------------------------------------
    //  Anónimos — el usuario todavía no tiene sesión.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Pide el correo de recuperación. La API responde 200 exista o no la cuenta —para que este
    /// formulario no sirva de oráculo para enumerar direcciones registradas— y esta redirección
    /// respeta esa cautela: siempre acaba en la misma pantalla, que dice lo mismo en los dos casos.
    /// </summary>
    public static async Task<IResult> ForgotPasswordAsync(
        [FromForm] EmailForm? form,
        AuthApiGateway       api,
        CancellationToken    ct)
    {
        form ??= new();

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/forgot-password",
            new { Email = form.Email ?? string.Empty }, authenticated: false, ct);

        // Solo se rompe la ambigüedad cuando el fallo NO habla de la cuenta: si la API no
        // respondió, callarlo dejaría al usuario esperando un correo que nadie llegó a pedir.
        return outcome.Success
            ? Results.Redirect(ForgotPasswordSentPage)
            : Failure(ForgotPasswordPage, outcome.ErrorCodeOr("PASSWORD_RESET_FAILED"));
    }

    /// <summary>
    /// Fija la contraseña nueva con el token del correo.
    /// </summary>
    /// <remarks>
    /// OJO CON EL IDENTIFICADOR. <c>ResetPassword.razor</c> postea <c>UserId</c>, pero
    /// <c>ResetPasswordRequest</c> de SignupAPI espera <c>Email</c> y su handler resuelve al
    /// usuario con <c>FindByEmailAsync</c>. Los dos contratos no coinciden y ninguno de los dos
    /// se toca en esta tarea, así que aquí se reenvía tal cual lo que trajo el enlace: es lo
    /// único que puede funcionar contra la API de hoy, cuyo propio <c>ForgotPasswordHandler</c>
    /// documenta el enlace como <c>?email=…&amp;token=…</c>. La página acepta las dos formas de
    /// la query por el mismo motivo. Está reportado: o la API pasa a aceptar userId, o el
    /// parámetro del componente pasa a llamarse Email.
    /// </remarks>
    public static async Task<IResult> ResetPasswordAsync(
        [FromForm] ResetPasswordForm? form,
        AuthApiGateway                api,
        CancellationToken             ct)
    {
        form ??= new();

        // El enlace se conserva en la vuelta: sin userId y token el formulario no se vuelve a
        // pintar, y el usuario tendría que ir a buscar el correo otra vez por haberse equivocado
        // al teclear la contraseña.
        var returnUrl = $"{ResetPasswordPage}?userId={Uri.EscapeDataString(form.UserId ?? string.Empty)}" +
                        $"&token={Uri.EscapeDataString(form.Token ?? string.Empty)}";

        if (!PasswordsMatch(form.NewPassword, form.ConfirmPassword))
            return Failure(returnUrl, "PASSWORD_RESET_FAILED");

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/reset-password",
            new
            {
                Email       = form.UserId ?? string.Empty,
                Token       = form.Token ?? string.Empty,
                NewPassword = form.NewPassword ?? string.Empty
            },
            authenticated: false, ct);

        return outcome.Success
            ? Results.Redirect(ResetPasswordDonePage)
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
        HttpContext       httpContext,
        AuthApiGateway    api,
        CancellationToken ct)
    {
        var email = EmailOfSession(httpContext);
        if (string.IsNullOrWhiteSpace(email))
            return Failure(ProfilePage, AuthApiGateway.SessionExpired);

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/email/send-confirmation",
            new { Email = email }, authenticated: false, ct);

        return outcome.Success
            ? Results.Redirect($"{ProfilePage}?resent=1")
            : Failure(ProfilePage, outcome.ErrorCodeOr("SEND_CONFIRMATION_FAILED"));
    }

    /// <summary>Cambia la contraseña de una cuenta que ya tiene una.</summary>
    public static async Task<IResult> ChangePasswordAsync(
        [FromForm] ChangePasswordForm? form,
        AuthApiGateway                 api,
        CancellationToken              ct)
    {
        form ??= new();

        if (!PasswordsMatch(form.NewPassword, form.ConfirmPassword))
            return Failure(PasswordPage, "PASSWORD_CHANGE_FAILED");

        var outcome = await api.CallAsync(
            HttpMethod.Put, "api/v1/auth/change-password",
            new
            {
                CurrentPassword = form.CurrentPassword ?? string.Empty,
                NewPassword     = form.NewPassword ?? string.Empty
            },
            authenticated: true, ct);

        return outcome.Success
            ? Results.Redirect(ProfilePage)
            : Failure(PasswordPage, outcome.ErrorCodeOr("PASSWORD_CHANGE_FAILED"));
    }

    /// <summary>Fija la primera contraseña de una cuenta que no tiene ninguna.</summary>
    public static async Task<IResult> SetPasswordAsync(
        [FromForm] SetPasswordForm? form,
        AuthApiGateway              api,
        CancellationToken           ct)
    {
        form ??= new();

        if (!PasswordsMatch(form.NewPassword, form.ConfirmPassword))
            return Failure(PasswordPage, "PASSWORD_SET_FAILED");

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/set-password",
            new { NewPassword = form.NewPassword ?? string.Empty }, authenticated: true, ct);

        return outcome.Success
            ? Results.Redirect(ProfilePage)
            : Failure(PasswordPage, outcome.ErrorCodeOr("PASSWORD_SET_FAILED"));
    }

    /// <summary>
    /// Da de alta el teléfono y manda el código que lo confirmará. El reto vuelve en cookie, no
    /// en la URL, por lo mismo que el del segundo factor del login.
    /// </summary>
    public static async Task<IResult> AddPhoneAsync(
        [FromForm] PhoneForm? form,
        AuthApiGateway        api,
        HttpContext           httpContext,
        CancellationToken     ct)
    {
        form ??= new();

        var outcome = await api.CallAsync<PhoneChallenge>(
            HttpMethod.Post, "api/v1/auth/phone",
            new { PhoneE164 = form.PhoneE164 ?? string.Empty }, authenticated: true, ct);

        if (!outcome.Success || outcome.Data is null ||
            string.IsNullOrWhiteSpace(outcome.Data.ChallengeToken))
        {
            return Failure(PhonePage, outcome.ErrorCodeOr("INVALID_PHONE"));
        }

        ChallengeCookies.Set(httpContext, ChallengeCookies.Phone, outcome.Data.ChallengeToken);

        // El destino sí va en la URL: llega ya enmascarado por la API (***4321) y no es una
        // credencial. Sin él, la pantalla de verificación no podría decir a qué número fue el
        // código, que es justo lo que permite darse cuenta de una cifra mal tecleada.
        var target = string.IsNullOrWhiteSpace(outcome.Data.MaskedTarget)
            ? string.Empty
            : $"?target={Uri.EscapeDataString(outcome.Data.MaskedTarget)}";

        return Results.Redirect($"{PhoneVerifyPage}{target}");
    }

    /// <summary>Confirma el teléfono con el código del SMS y el reto que viaja en cookie.</summary>
    public static async Task<IResult> VerifyPhoneAsync(
        [FromForm] CodeForm? form,
        AuthApiGateway       api,
        HttpContext          httpContext,
        CancellationToken    ct)
    {
        form ??= new();

        var challengeToken = ChallengeCookies.Read(httpContext, ChallengeCookies.Phone);
        if (string.IsNullOrWhiteSpace(challengeToken))
        {
            // Sin reto no hay nada que canjear: caducó o alguien entró directo a la URL. La
            // salida no es el login —el usuario sigue dentro— sino volver a empezar el alta.
            return Failure(PhonePage, "INVALID_CHALLENGE");
        }

        var outcome = await api.CallAsync(
            HttpMethod.Post, "api/v1/auth/phone/verify",
            new { ChallengeToken = challengeToken, Code = form.Code ?? string.Empty },
            authenticated: true, ct);

        if (outcome.Success)
        {
            ChallengeCookies.Delete(httpContext, ChallengeCookies.Phone);
            return Results.Redirect(ProfilePage);
        }

        // Un reto inválido o agotado ya no sirve para nada: fuera la cookie, y de vuelta al alta.
        var code = outcome.ErrorCodeOr("CODE_INVALID");
        if (code is "INVALID_CHALLENGE" or "CODE_EXPIRED" or "TOO_MANY_ATTEMPTS")
        {
            ChallengeCookies.Delete(httpContext, ChallengeCookies.Phone);
            return Failure(PhonePage, code);
        }

        return Failure(PhoneVerifyPage, code);
    }

    /// <summary>
    /// Da de baja el teléfono. Llega ya confirmado por el usuario: el enlace de ManageIndex no
    /// postea nada, solo repinta la pantalla con el aviso y es ese aviso el que trae este botón.
    /// </summary>
    public static async Task<IResult> RemovePhoneAsync(
        AuthApiGateway    api,
        HttpContext       httpContext,
        CancellationToken ct)
    {
        var outcome = await api.CallAsync(
            HttpMethod.Delete, "api/v1/auth/phone", body: null, authenticated: true, ct);

        // Un alta a medias con el teléfono ya borrado es un reto que apunta a un número que ya no
        // está en la cuenta.
        ChallengeCookies.Delete(httpContext, ChallengeCookies.Phone);

        return outcome.Success
            ? Results.Redirect(ProfilePage)
            : Failure(ProfilePage, outcome.ErrorCodeOr("PHONE_NOT_FOUND"));
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
        AuthApiGateway     api,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory     loggerFactory,
        CancellationToken  ct)
    {
        var token = api.AccessToken;
        if (string.IsNullOrWhiteSpace(token))
            return Failure(ProfilePage, AuthApiGateway.SessionExpired);

        try
        {
            var httpClient = httpClientFactory.CreateClient("AuthApi");
            using var request = new HttpRequestMessage(
                HttpMethod.Get, "api/v1/auth/personal-data/download");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return Failure("/admin/account/personal-data", "DOWNLOAD_FAILED");

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
            return Failure("/admin/account/personal-data", AuthApiGateway.Unreachable);
        }
    }

    // ---------------------------------------------------------------------------------------
    //  Lo común
    // ---------------------------------------------------------------------------------------

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
    /// .NET, igual que hace <see cref="AuthEndpoints"/> con los roles.
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

    /// <summary>Respuesta de <c>POST /api/v1/auth/phone</c>, recortada a lo que se usa.</summary>
    private sealed record PhoneChallenge
    {
        public string ChallengeToken { get; init; } = string.Empty;
        public string MaskedTarget   { get; init; } = string.Empty;
    }
}
