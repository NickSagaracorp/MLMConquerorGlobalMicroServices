using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using MLMConquerorGlobalEdition.BizCenterWeb.Components;
using MLMConquerorGlobalEdition.BizCenterWeb.Middleware;
using MLMConquerorGlobalEdition.BizCenterWeb.Services;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Services;
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
builder.Services.AddTransient<BizCenterApiAuthHandler>();

// Nombres de las cookies de reto de ESTE portal. Siguen la convención de su cookie de sesión
// (mlm_bizcenter_cookie) y son distintos de los de administración a propósito: con Path = "/" y un
// mismo dominio para los dos portales, unos nombres compartidos harían que el reto de uno pisara el
// del otro. Se declaran aquí, en un solo sitio, porque el mismo juego lo usan los manejadores de
// login de este portal y —cuando se monte— el área de cuenta compartida.
builder.Services.AddChallengeCookieNames(new ChallengeCookieNames
{
    Login      = "mlm_bizcenter_2fa_challenge",
    Enrollment = "mlm_bizcenter_2fa_enrollment",
    Phone      = "mlm_bizcenter_phone_challenge"
});

// Server-side auth state provider (persists to WASM client)
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthStateProvider>();

// Shared components
builder.Services.AddSharedComponents();
builder.Services.AddScoped<ServerViewContextInitializer>();

// HTTP client — BizCenter authenticated API.
// BizCenterApiAuthHandler forwards the JWT from the HttpOnly cookie claim to the API.
builder.Services.AddHttpClient("BizCenterApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7003");
}).AddHttpMessageHandler<BizCenterApiAuthHandler>();
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
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MLMConquerorGlobalEdition.BizCenterWeb.Client._Imports).Assembly);

// Auth endpoints — antiforgery disabled (login = unauthenticated form POST)
app.MapPost("/account/login",  (Delegate)AuthEndpoints.LoginAsync).DisableAntiforgery();
app.MapPost("/account/logout", (Delegate)AuthEndpoints.LogoutAsync).DisableAntiforgery();
app.MapGet("/account/logout",  (Delegate)AuthEndpoints.LogoutAsync);

// 2FA challenge endpoints — antiforgery disabled (form posts on
// unauthenticated /two-factor page, identity carried in HttpOnly cookie).
app.MapPost("/account/two-factor/verify", (Delegate)AuthEndpoints.VerifyTwoFactorAsync).DisableAntiforgery();
app.MapPost("/account/two-factor/resend", (Delegate)AuthEndpoints.ResendTwoFactorAsync).DisableAntiforgery();

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
