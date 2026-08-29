using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Lo que las páginas del área de cuenta necesitan para pintarse y que no cabe en un formulario:
/// el estado de la cuenta y el resultado de redimir el enlace de confirmación de correo.
///
/// Mismo reparto que <see cref="TwoFactorPageData"/> frente a <c>AuthEndpoints</c>: aquello son
/// manejadores de POST —una acción que acaba en redirección—; esto se ejecuta durante el render de
/// una página, que es un GET. Un endpoint intermedio cuyo único trabajo fuera devolverle a la
/// página datos que la página puede pedir directamente no aportaría nada.
///
/// Con ámbito de petición y con el resultado memorizado: <c>account-status</c> se pide UNA vez
/// por página aunque la pinten tres componentes. La llamada sale del servidor porque el token de
/// acceso vive en un claim de la cookie de sesión, que es <c>HttpOnly</c>; las páginas que lo
/// usan se renderizan en modo estático (SSR), sin <c>@@rendermode</c>, igual que
/// <c>Login.razor</c>, así que ahí el <c>HttpContext</c> está disponible.
/// </summary>
public sealed class AccountPageData
{
    private readonly AuthApiGateway            _api;
    private readonly IHttpContextAccessor      _httpContextAccessor;
    private readonly IConfiguration            _configuration;
    private readonly ChallengeCookieNames      _challengeCookies;
    private readonly ILogger<AccountPageData>  _logger;

    /// <summary>
    /// El resultado de la única llamada a <c>account-status</c> de esta petición. Memorizado y no
    /// recalculado: sin esto, una página que monta AccountLayout, ManageIndex y el panel del
    /// segundo factor haría tres viajes idénticos a la API para pintar una sola pantalla — y
    /// podrían llegar tres respuestas distintas si algo cambia entre medias.
    /// </summary>
    private AccountStatus? _status;
    private string?        _statusErrorCode;
    private bool           _statusLoaded;

    public AccountPageData(
        AuthApiGateway           api,
        IHttpContextAccessor     httpContextAccessor,
        IConfiguration           configuration,
        ChallengeCookieNames     challengeCookies,
        ILogger<AccountPageData> logger)
    {
        _api                 = api;
        _httpContextAccessor = httpContextAccessor;
        _configuration       = configuration;
        _challengeCookies    = challengeCookies;
        _logger              = logger;
    }

    /// <summary>
    /// Estado de la cuenta del usuario de la sesión, o null si la API no lo pudo dar. En ese caso
    /// <see cref="StatusErrorCode"/> dice por qué, para que la página enseñe un mensaje en vez de
    /// reventar con una referencia nula.
    /// </summary>
    public async Task<AccountStatus?> GetStatusAsync(CancellationToken ct = default)
    {
        if (_statusLoaded)
            return _status;

        _statusLoaded = true;

        var outcome = await _api.CallAsync<AccountStatus>(
            HttpMethod.Get, "api/v1/auth/account-status", body: null, authenticated: true, ct);

        if (!outcome.Success || outcome.Data is null)
        {
            _statusErrorCode = outcome.ErrorCodeOr(AuthApiGateway.Unreachable);
            _logger.LogWarning(
                "No se pudo leer el estado de la cuenta ({ErrorCode}); la página se pintará degradada.",
                _statusErrorCode);
            return null;
        }

        _status = outcome.Data;
        return _status;
    }

    /// <summary>Por qué falló <see cref="GetStatusAsync"/>. Null mientras no haya fallado.</summary>
    public string? StatusErrorCode => _statusErrorCode;

    /// <summary>
    /// Redime el enlace de confirmación de correo y devuelve el estado que espera
    /// <c>ConfirmEmail.Status</c>.
    /// </summary>
    /// <remarks>
    /// La API no distingue "caducado" de "inválido" con un código propio: Identity rechaza los
    /// dos casos dentro de <c>ConfirmEmailAsync</c> y el handler los devuelve como
    /// <c>EMAIL_CONFIRMATION_FAILED</c>. Ese es el único que se traduce a "caducado", que es de
    /// largo el motivo más frecuente de que un enlace bien formado deje de servir; un enlace
    /// truncado o con un userId que no existe llega con otro código y se trata como inválido. Al
    /// usuario le cambia poco: en los dos casos el siguiente paso es pedir uno nuevo.
    /// </remarks>
    public async Task<string> ConfirmEmailAsync(
        string? userId, string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return AccountMessages.ConfirmEmailInvalid;

        var outcome = await _api.CallAsync(
            HttpMethod.Post, "api/v1/auth/email/confirm",
            new { UserId = userId, Token = token }, authenticated: false, ct);

        if (outcome.Success)
            return AccountMessages.ConfirmEmailSuccess;

        return outcome.ErrorCode == "EMAIL_CONFIRMATION_FAILED"
            ? AccountMessages.ConfirmEmailExpired
            : AccountMessages.ConfirmEmailInvalid;
    }

    /// <summary>
    /// ¿Hay un alta de teléfono a medias, con su reto todavía vivo?
    /// </summary>
    /// <remarks>
    /// La pantalla de verificación lo pregunta antes de pintarse. Sin reto no hay nada que
    /// canjear —caducó, o el usuario llegó desde el perfil con un teléfono sin confirmar de otra
    /// sesión— y el único sitio que emite uno nuevo es <c>POST /api/v1/auth/phone</c>, así que la
    /// salida es volver al alta en vez de enseñar un formulario que fallaría al enviarse.
    /// </remarks>
    public bool HasPhoneChallenge() =>
        !string.IsNullOrWhiteSpace(
            ChallengeCookies.Read(_httpContextAccessor.HttpContext, _challengeCookies.Phone));

    /// <summary>
    /// ¿El rol del usuario le obliga a tener segundo factor? Si sí, el panel no ofrece apagarlo.
    /// </summary>
    /// <remarks>
    /// Se resuelve comparando los roles del token con <c>Auth:TwoFactor:MandatoryRoles</c>, que es
    /// exactamente lo que hace <c>LoginHandler</c> en SignupAPI para decidir si fuerza el
    /// enrolamiento. Las dos listas tienen que ser la misma: si el portal dijera que el rol no lo
    /// exige y el servidor sí, el usuario apagaría el segundo factor y se quedaría fuera en su
    /// siguiente inicio de sesión, cuando el login le exigiera enrolarse otra vez.
    ///
    /// La clave de configuración es la misma en los dos portales a propósito: es la lista del
    /// servidor la que manda, y un portal que leyera otra sección volvería a abrir justo esa
    /// discrepancia.
    /// </remarks>
    public bool TwoFactorRequiredByRole()
    {
        var mandatoryRoles = _configuration
            .GetSection("Auth:TwoFactor:MandatoryRoles").Get<string[]>() ?? [];

        if (mandatoryRoles.Length == 0)
            return false;

        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
            return false;

        // Los dos nombres del claim de rol: el corto que emite el JWT y el largo de .NET. Igual
        // que en AuthEndpoints — mirar solo uno deja fuera la mitad de los tokens.
        var roles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role ||
                        c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" ||
                        c.Type == "role")
            .Select(c => c.Value);

        return roles.Any(r => mandatoryRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// ¿La cuenta tiene perfil de miembro? El personal interno no tiene, y para ellos
    /// <c>PersonalDataResponse.MemberProfile</c> viene null.
    /// </summary>
    /// <remarks>
    /// Sale del claim <c>memberId</c> del token y no de una llamada: el handler de datos
    /// personales decide exactamente eso —<c>if (!string.IsNullOrEmpty(user.MemberProfileId))</c>—
    /// y ese identificador ya viaja en el token. Pedir el archivo entero de datos personales para
    /// quedarse con un booleano sería mover toda la PII del usuario por la red a cambio de nada.
    /// </remarks>
    public bool HasMemberProfile()
    {
        var memberId = _httpContextAccessor.HttpContext?.User.FindFirstValue("memberId");
        return !string.IsNullOrWhiteSpace(memberId);
    }

    /// <summary>
    /// <c>GET /api/v1/auth/account-status</c>, recortado a lo que pintan los componentes.
    /// </summary>
    /// <remarks>
    /// Los canales llegan como texto porque SignupAPI serializa los enums con
    /// <c>JsonStringEnumConverter</c>, y como texto es como los quieren <c>TwoFactorPanel</c> y
    /// <c>TwoFactorVerify</c> — ninguno de los dos referencia Domain. Sin esa configuración esto
    /// llegaría como número y habría que mapearlo aquí.
    /// </remarks>
    public sealed record AccountStatus
    {
        public string  Email          { get; init; } = string.Empty;
        public bool    EmailConfirmed { get; init; }

        public string? MaskedPhone    { get; init; }
        public bool    HasPhone       { get; init; }
        public bool    PhoneConfirmed { get; init; }

        public bool      TwoFactorEnabled          { get; init; }
        public string?   PreferredTwoFactorChannel { get; init; }
        public DateTime? TwoFactorEnrolledAt       { get; init; }

        public bool HasPassword { get; init; }

        public IReadOnlyList<string> AvailableChannels { get; init; } = [];
    }
}
