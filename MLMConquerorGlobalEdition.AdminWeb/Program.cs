using Microsoft.AspNetCore.Authentication.Cookies;
using MLMConquerorGlobalEdition.AdminWeb.Middleware;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using MLMConquerorGlobalEdition.AdminWeb.Components;
using MLMConquerorGlobalEdition.AdminWeb.Services;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Localization — supported cultures
var supportedCultures = new[] { "en", "es", "pt", "fr", "it", "ko", "de", "ru" };

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

// Server-side auth state provider (persists to WASM client)
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthStateProvider>();

// Shared components
builder.Services.AddSharedComponents();
builder.Services.AddScoped<ServerViewContextInitializer>();

// HTTP client to AdminAPI — attaches JWT Bearer token automatically
builder.Services.AddHttpClient("AdminApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7002");
}).AddHttpMessageHandler<AdminApiAuthHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
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
    .AddAdditionalAssemblies(typeof(MLMConquerorGlobalEdition.AdminWeb.Client._Imports).Assembly);

// Auth endpoints — antiforgery disabled on both (login = unauthenticated, logout = trivial)
app.MapPost("/account/login",  (Delegate)AuthEndpoints.LoginAsync).DisableAntiforgery();
app.MapPost("/account/logout", (Delegate)AuthEndpoints.LogoutAsync).DisableAntiforgery();

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
