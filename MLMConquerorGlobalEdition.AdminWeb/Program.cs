using Microsoft.AspNetCore.Authentication.Cookies;
using MLMConquerorGlobalEdition.AdminWeb.Middleware;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using MLMConquerorGlobalEdition.AdminWeb.Components;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;
using MLMConquerorGlobalEdition.SharedKernel;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Localization — the 9 supported cultures, mapped from app codes by LanguageCodeMapper.
var supportedCultures = LanguageCodeMapper.SupportedCultureNames.ToArray();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
    })
    .AddInteractiveWebAssemblyComponents();

// Auth — cookie-based for web (JWT stored in HttpOnly cookie)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath          = "/admin/login";
        options.LogoutPath         = "/admin/logout";
        options.ExpireTimeSpan     = TimeSpan.FromHours(8);
        options.SlidingExpiration  = true;
        options.Cookie.Name        = "mlm_admin_cookie";
        options.Cookie.HttpOnly     = true;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        options.Cookie.SameSite     = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
    });

builder.Services.AddAuthorization();
builder.Services.AddSyncfusionBlazor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// El manejador que lleva el JWT del usuario a las APIs de este portal. Es el mismo de los dos
// portales; lo único de aquí es a dónde se manda al usuario cuando su sesión caduca —a la salida,
// que limpia la cookie y redirige a esta pantalla de login con el aviso— en vez de dejarle en la
// cara el 401 crudo de la llamada que estaba en vuelo.
builder.Services.AddPortalApiAuthHandler("/admin/login");

// Los nombres de las cookies de reto de ESTE portal. Los usan tanto los manejadores compartidos del
// área de cuenta como los de la puerta (login y segundo factor), y por eso se declaran en un solo
// sitio: el que escribe el reto y el que lo lee tienen que estar mirando el mismo nombre.
var challengeCookieNames = new ChallengeCookieNames
{
    Login      = "mlm_admin_2fa_challenge",
    Enrollment = "mlm_admin_2fa_enrollment",
    Phone      = "mlm_admin_phone_challenge"
};

// Área de cuenta — el cableado (gateway a SignupAPI, carga de datos de página y manejadores de
// formulario) vive en SharedComponents y lo montan también otros portales. Lo único que cambia de
// uno a otro es dónde están sus pantallas, y eso es exactamente lo que se pasa aquí.
builder.Services.AddAccountSurface(new AccountPageRoutes
{
    ForgotPasswordPage     = "/admin/forgot-password",
    ForgotPasswordSentPage = "/admin/forgot-password/sent",
    ResetPasswordPage      = "/admin/reset-password",
    ResetPasswordDonePage  = "/admin/reset-password/done",
    ProfilePage            = "/admin/account",
    PasswordPage           = "/admin/account/password",
    PhonePage              = "/admin/account/phone",
    PhoneVerifyPage        = "/admin/account/phone/verify",
    SecurityPage           = "/admin/account/security",
    PersonalDataPage       = "/admin/account/personal-data"
},
challengeCookieNames);

// La puerta — login, segundo factor, enrolamiento forzado y salida. Los manejadores también son
// compartidos; de este portal son solo los destinos y quién tiene permitido entrar.
builder.Services.AddAuthSurface(new AuthPortalOptions
{
    LoginPage               = "/admin/login",
    TwoFactorPage           = "/admin/login-2fa",
    EnrollAuthenticatorPage = "/admin/enroll-authenticator",
    HomePage                = "/admin",

    // Administración es un portal de personal: la comprobación se hace sobre el token final, en el
    // único sitio donde se firma la sesión, así que da igual si el usuario llegó por el login
    // directo, por el segundo factor o por el enrolamiento.
    AllowedRoles =
    [
        "SuperAdmin", "Admin", "CommissionManager",
        "BillingManager", "SupportManager",
        "SupportLevel1", "SupportLevel2", "SupportLevel3", "IT"
    ]

    // FollowsMemberLanguage se queda apagado: el claim default_language solo lo emite SignupAPI
    // para cuentas con MemberProfile, y voltear el idioma del portal de administración a un
    // administrador que además sea miembro no es lo que este portal hacía.
},
challengeCookieNames);

// Server-side auth state provider (persists to WASM client)
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthStateProvider>();

// Shared components
builder.Services.AddSharedComponents();
// La mitad web del contexto de vista: ViewContextService se auto-inicializa desde el usuario y
// la ruta de la peticion. En una MAUI no hay peticion y esa semilla se queda vacia, que es
// justo por lo que la lectura del HttpContext ya no vive dentro de ViewContextService.
builder.Services.AddHttpContextViewContextSeed();
// Administración mira siempre en contexto de administrador; ese booleano es lo único que separa
// este inicializador del del centro de negocios.
builder.Services.AddServerViewContextInitializer(isAdminContext: true);

// El cliente a SignupAPI. Sin manejador que autentique —el login es anónimo por definición— y con
// las cookies del manejador APAGADAS, que es lo que impide que el refresh token de un usuario salga
// enganchado en la llamada de otro. Ese detalle vive en AddAuthApiClient y no aquí a propósito: los
// dos portales tienen que registrarlo igual.
builder.Services.AddAuthApiClient(
    builder.Configuration["AuthApiBaseUrl"] ?? "https://localhost:7005");

// HTTP client to AdminAPI — attaches JWT Bearer token automatically
builder.Services.AddHttpClient("AdminApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7002");
}).AddHttpMessageHandler<ApiAuthHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi"));

// HTTP client to TicketManagementSystem — attaches JWT Bearer token automatically
builder.Services.AddHttpClient("HelpdeskApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HelpdeskApiUrl"] ?? "http://localhost:5045");
}).AddHttpMessageHandler<ApiAuthHandler>();

// HTTP client to RankEngine — attaches JWT Bearer token automatically
builder.Services.AddHttpClient("RankEngineApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["RankEngineApiBaseUrl"] ?? "https://localhost:7009");
}).AddHttpMessageHandler<ApiAuthHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(LanguageCodeMapper.DefaultCultureName)
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));
app.UseAntiforgery();
app.UseAuthentication();
// Justo después de la autenticación, que es cuando ya hay ClaimsPrincipal y todavía no ha empezado
// la respuesta: una navegación de un usuario cuyo JWT ya caducó se corta aquí, se le limpia la
// cookie y se le manda al login con el aviso. Dentro del circuito ese trabajo lo hace ApiAuthHandler;
// esto cubre lo que aquello no puede ver: recargas, marcadores y el primer render de un circuito.
app.UseSessionExpiry();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MLMConquerorGlobalEdition.AdminWeb.Client._Imports).Assembly);

// ── La puerta ───────────────────────────────────────────────────────────────────────────────
// Los manejadores viven en SharedComponents.Server; las RUTAS se quedan aquí porque tienen que
// coincidir letra a letra con el action= del formulario de cada pantalla de este portal.
// Antiforgery desactivado en todos: son anónimos por definición, o un logout trivial.
app.MapPost("/account/login",  (Delegate)AuthEndpoints.LoginAsync).DisableAntiforgery();
app.MapPost("/account/logout", (Delegate)AuthEndpoints.LogoutAsync).DisableAntiforgery();
app.MapGet("/account/logout",  (Delegate)AuthEndpoints.LogoutAsync);

// Segundo factor y enrolamiento — el reto viaja en cookie HttpOnly, nunca en la URL ni en el formulario
app.MapPost("/account/login-2fa",           (Delegate)AuthEndpoints.LoginTwoFactorAsync).DisableAntiforgery();
app.MapPost("/account/login-2fa/resend",    (Delegate)AuthEndpoints.ResendTwoFactorAsync).DisableAntiforgery();
app.MapPost("/account/enroll-authenticator",(Delegate)AuthEndpoints.EnrollAuthenticatorAsync).DisableAntiforgery();

// ── Área de cuenta ──────────────────────────────────────────────────────────────────────────
// Las mismas rutas de siempre, montadas desde SharedComponents. Cuál lleva RequireAuthorization()
// y cuál no vive allí junto a los manejadores: es una decisión de seguridad que no se ve desde la
// pantalla, y copiarla en cada portal es pedir que a uno se le olvide.
app.MapAccountEndpoints();

// Culture selection endpoint — sets cookie and redirects back
app.MapGet("/culture", (HttpContext ctx, string culture, string redirectUri) =>
{
    if (!string.IsNullOrWhiteSpace(culture))
    {
        ctx.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture, culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), SameSite = SameSiteMode.Lax }
        );
    }
    var target = "/admin";
    if (!string.IsNullOrWhiteSpace(redirectUri))
    {
        if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var abs))
            target = abs.PathAndQuery;
        else if (redirectUri.StartsWith("/"))
            target = redirectUri;
    }
    return Results.LocalRedirect(target);
});

app.Run();
