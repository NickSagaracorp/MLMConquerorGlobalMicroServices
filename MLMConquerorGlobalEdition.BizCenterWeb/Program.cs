using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using MLMConquerorGlobalEdition.BizCenterWeb.Components;
using MLMConquerorGlobalEdition.BizCenterWeb.Services;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Localization — supported cultures
var supportedCultures = new[] { "en", "es", "pt", "fr", "it", "ko", "de", "ru" };

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
        options.Cookie.HttpOnly    = true;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Server-side auth state provider (persists to WASM client)
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthStateProvider>();

// Shared components
builder.Services.AddSharedComponents();
builder.Services.AddScoped<ServerViewContextInitializer>();

// HTTP client — BizCenter authenticated API
builder.Services.AddHttpClient("BizCenterApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7001");
});
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("BizCenterApi"));

// HTTP client — Signups public API (unauthenticated signup wizard)
builder.Services.AddHttpClient("SignupsApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["SignupsApiBaseUrl"] ?? "https://localhost:7147");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
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

// Auth endpoints
app.MapPost("/account/login",  AuthEndpoints.LoginAsync);
app.MapPost("/account/logout", AuthEndpoints.LogoutAsync);

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
