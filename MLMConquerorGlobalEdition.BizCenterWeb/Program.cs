using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using MLMConquerorGlobalEdition.BizCenterWeb.Components;
using MLMConquerorGlobalEdition.BizCenterWeb.Middleware;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;
using MLMConquerorGlobalEdition.SharedKernel;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Localization — the 9 supported cultures, mapped from app codes by LanguageCodeMapper.
var supportedCultures = LanguageCodeMapper.SupportedCultureNames.ToArray();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Auth — cookie-based for web (JWT stored in HttpOnly cookie)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath          = "/login";
        options.LogoutPath         = "/logout";
        options.ExpireTimeSpan     = TimeSpan.FromHours(24);
        options.SlidingExpiration  = true;
        options.Cookie.Name        = "mlm_bizcenter_cookie";
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
builder.Services.AddPortalApiAuthHandler("/login");

// Nombres de las cookies de reto de ESTE portal. Siguen la convención de su cookie de sesión
// (mlm_bizcenter_cookie) y son distintos de los de administración a propósito: con Path = "/" y un
// mismo dominio para los dos portales, unos nombres compartidos harían que el reto de uno pisara el
// del otro. Se declaran aquí, en un solo sitio, porque el mismo juego lo usan los manejadores de
// login de este portal y —cuando se monte— el área de cuenta compartida.
var challengeCookieNames = new ChallengeCookieNames
{
    Login      = "mlm_bizcenter_2fa_challenge",
    Enrollment = "mlm_bizcenter_2fa_enrollment",
    Phone      = "mlm_bizcenter_phone_challenge"
};

// Área de cuenta — el cableado (gateway a SignupAPI, carga de datos de página y manejadores de
// formulario) vive en SharedComponents y lo monta también administración. Lo único que cambia de un
// portal a otro es dónde están sus pantallas, y eso es exactamente lo que se pasa aquí.
//
// ESTE PORTAL NO TIENE PREFIJO, y eso obliga a una decisión que administración no tuvo que tomar:
// allí las pantallas cuelgan de /admin/account/... y los POST del área de /account/..., así que las
// dos familias no pueden chocar. Aquí comparten raíz. La única colisión real es la verificación del
// teléfono —MapAccountEndpoints sirve POST /account/phone/verify—, así que su PANTALLA se llama
// /account/phone/confirm. Las demás no coinciden con ningún endpoint.
builder.Services.AddAccountSurface(new AccountPageRoutes
{
    ForgotPasswordPage     = "/forgot-password",
    ForgotPasswordSentPage = "/forgot-password/sent",
    ResetPasswordPage      = "/reset-password",
    ResetPasswordDonePage  = "/reset-password/done",
    ProfilePage            = "/account",
    PasswordPage           = "/account/password",
    PhonePage              = "/account/phone",
    PhoneVerifyPage        = "/account/phone/confirm",
    SecurityPage           = "/account/security",
    PersonalDataPage       = "/account/personal-data"
},
challengeCookieNames);

// La puerta — login, segundo factor, enrolamiento forzado y salida. Los manejadores son los mismos
// que los de administración y viven en SharedComponents.Server; de este portal son solo los
// destinos.
builder.Services.AddAuthSurface(new AuthPortalOptions
{
    LoginPage     = "/login",
    TwoFactorPage = "/two-factor",

    // La pantalla del enrolamiento forzado, a la que va el miembro cuyo rol exige segundo factor y
    // todavía no lo tiene configurado. Va en la raíz, como /two-factor.
    EnrollAuthenticatorPage = "/enroll-authenticator",

    HomePage = "/",

    // Sin lista de roles: el centro de negocios admite a cualquier cuenta válida.

    // El idioma preferido del miembro viaja en el claim default_language del token; fijarlo aquí
    // hace que un primer inicio de sesión en un dispositivo nuevo ya aterrice en su idioma.
    FollowsMemberLanguage = true
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
// El centro de negocios nunca mira en contexto de administrador; ese booleano es lo único que
// separa este inicializador del de administración.
builder.Services.AddServerViewContextInitializer(isAdminContext: false);

// HTTP client — BizCenter authenticated API.
// ApiAuthHandler forwards the JWT from the HttpOnly cookie claim to the API.
builder.Services.AddHttpClient("BizCenterApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7003");
}).AddHttpMessageHandler<ApiAuthHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("BizCenterApi"));

// HTTP client — Auth (SignupAPI handles auth for all apps)
builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AuthApiBaseUrl"] ?? "https://localhost:7005");
});

// HTTP client — Signups public API (unauthenticated signup wizard)
builder.Services.AddHttpClient("SignupsApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["SignupsApiBaseUrl"] ?? "https://localhost:7005");
});

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
    .AddAdditionalAssemblies(typeof(MLMConquerorGlobalEdition.BizCenterWeb.Client._Imports).Assembly);

// ── La puerta ───────────────────────────────────────────────────────────────────────────────
// Los manejadores viven en SharedComponents.Server; las RUTAS se quedan aquí porque tienen que
// coincidir letra a letra con el action= del formulario de cada pantalla de este portal.
// Antiforgery desactivado en todos: son anónimos por definición, o un logout trivial.
app.MapPost("/account/login",  (Delegate)AuthEndpoints.LoginAsync).DisableAntiforgery();
app.MapPost("/account/logout", (Delegate)AuthEndpoints.LogoutAsync).DisableAntiforgery();
app.MapGet("/account/logout",  (Delegate)AuthEndpoints.LogoutAsync);

// Segundo factor y enrolamiento — el reto viaja en cookie HttpOnly, nunca en la URL ni en el
// formulario.
app.MapPost("/account/two-factor/verify",   (Delegate)AuthEndpoints.LoginTwoFactorAsync).DisableAntiforgery();
app.MapPost("/account/two-factor/resend",   (Delegate)AuthEndpoints.ResendTwoFactorAsync).DisableAntiforgery();
app.MapPost("/account/enroll-authenticator",(Delegate)AuthEndpoints.EnrollAuthenticatorAsync).DisableAntiforgery();

// ── Área de cuenta ──────────────────────────────────────────────────────────────────────────
// Las mismas rutas que sirve administración, montadas desde SharedComponents. Cuál lleva
// RequireAuthorization() y cuál no vive allí junto a los manejadores: es una decisión de seguridad
// que no se ve desde la pantalla, y copiarla en cada portal es pedir que a uno se le olvide.
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
    var target = "/";
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
