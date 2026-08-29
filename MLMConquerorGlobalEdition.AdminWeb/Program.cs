using Microsoft.AspNetCore.Authentication.Cookies;
using MLMConquerorGlobalEdition.AdminWeb.Middleware;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using MLMConquerorGlobalEdition.AdminWeb.Components;
using MLMConquerorGlobalEdition.AdminWeb.Services;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Services;
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
    PersonalDataPage       = "/admin/account/personal-data"
});

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
