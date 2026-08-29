using Microsoft.AspNetCore.Authentication.Cookies;
using MLMConquerorGlobalEdition.AdminWeb.Middleware;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using MLMConquerorGlobalEdition.AdminWeb.Components;
using MLMConquerorGlobalEdition.AdminWeb.Services;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
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
builder.Services.AddTransient<AdminApiAuthHandler>();

// Datos que las páginas del segundo factor piden durante el render (canal del reto, QR y clave
// del enrolamiento). Scoped: depende del HttpContext de la petición, que es de donde salen las
// cookies HttpOnly del reto.
builder.Services.AddScoped<TwoFactorPageData>();

// La única puerta a SignupAPI desde este portal: monta la llamada, le pone el Bearer del claim
// access_token cuando hace falta, desenvuelve el ApiResponse y traduce el fallo a un código.
// Scoped porque el token sale del HttpContext de la petición.
builder.Services.AddScoped<AuthApiGateway>();

// Datos que las páginas del área de cuenta piden durante el render. Scoped y con el resultado
// memorizado: account-status se pide UNA vez por página aunque la pinten tres componentes.
builder.Services.AddScoped<AccountPageData>();

// Server-side auth state provider (persists to WASM client)
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthStateProvider>();

// Shared components
builder.Services.AddSharedComponents();
builder.Services.AddScoped<ServerViewContextInitializer>();

// HTTP client to SignupAPI — auth only, no auth handler (login is unauthenticated)
builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AuthApiBaseUrl"] ?? "https://localhost:7005");
});

// HTTP client to AdminAPI — attaches JWT Bearer token automatically
builder.Services.AddHttpClient("AdminApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7002");
}).AddHttpMessageHandler<AdminApiAuthHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi"));

// HTTP client to TicketManagementSystem — attaches JWT Bearer token automatically
builder.Services.AddHttpClient("HelpdeskApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HelpdeskApiUrl"] ?? "http://localhost:5045");
}).AddHttpMessageHandler<AdminApiAuthHandler>();

// HTTP client to RankEngine — attaches JWT Bearer token automatically
builder.Services.AddHttpClient("RankEngineApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["RankEngineApiBaseUrl"] ?? "https://localhost:7009");
}).AddHttpMessageHandler<AdminApiAuthHandler>();

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
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MLMConquerorGlobalEdition.AdminWeb.Client._Imports).Assembly);

// Auth endpoints — antiforgery disabled on all (unauthenticated by definition, o logout trivial)
app.MapPost("/account/login",  (Delegate)AuthEndpoints.LoginAsync).DisableAntiforgery();
app.MapPost("/account/logout", (Delegate)AuthEndpoints.LogoutAsync).DisableAntiforgery();
app.MapGet("/account/logout",  (Delegate)AuthEndpoints.LogoutAsync);

// Segundo factor y enrolamiento — el reto viaja en cookie HttpOnly, nunca en la URL ni en el formulario
app.MapPost("/account/login-2fa",           (Delegate)AuthEndpoints.LoginTwoFactorAsync).DisableAntiforgery();
app.MapPost("/account/login-2fa/resend",    (Delegate)AuthEndpoints.ResendTwoFactorAsync).DisableAntiforgery();
app.MapPost("/account/enroll-authenticator",(Delegate)AuthEndpoints.EnrollAuthenticatorAsync).DisableAntiforgery();

// ── Área de cuenta ──────────────────────────────────────────────────────────────────────────
// Antiforgery desactivado como en los de arriba: son formularios HTML posteados desde páginas en
// SSR estático, sin circuito interactivo que pueda llevar el token.
//
// Anónimos — el usuario todavía no tiene sesión.
app.MapPost("/account/forgot-password",     (Delegate)AccountEndpoints.ForgotPasswordAsync).DisableAntiforgery();
app.MapPost("/account/reset-password",      (Delegate)AccountEndpoints.ResetPasswordAsync).DisableAntiforgery();

// De gestión — el Bearer sale del claim access_token de la cookie, que AuthApiGateway lee del
// HttpContext. RequireAuthorization() los cierra antes de que el manejador llegue a correr: sin
// él, una llamada sin sesión acabaría en el manejador y volvería con SESSION_EXPIRED en la URL en
// vez de mandar al login, que es lo que el usuario necesita.
app.MapPost("/account/resend-confirmation", (Delegate)AccountEndpoints.ResendConfirmationAsync).DisableAntiforgery().RequireAuthorization();
app.MapPost("/account/change-password",     (Delegate)AccountEndpoints.ChangePasswordAsync).DisableAntiforgery().RequireAuthorization();
app.MapPost("/account/set-password",        (Delegate)AccountEndpoints.SetPasswordAsync).DisableAntiforgery().RequireAuthorization();
app.MapPost("/account/phone/add",           (Delegate)AccountEndpoints.AddPhoneAsync).DisableAntiforgery().RequireAuthorization();
app.MapPost("/account/phone/verify",        (Delegate)AccountEndpoints.VerifyPhoneAsync).DisableAntiforgery().RequireAuthorization();
app.MapPost("/account/phone/remove",        (Delegate)AccountEndpoints.RemovePhoneAsync).DisableAntiforgery().RequireAuthorization();

// GET y no POST: lo pide un <a href> de PersonalData.razor. Hace de intermediaria porque el
// endpoint de la API exige Bearer y el navegador no lo lleva en un enlace normal.
app.MapGet("/account/personal-data/download", (Delegate)AccountEndpoints.DownloadPersonalDataAsync).RequireAuthorization();

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
