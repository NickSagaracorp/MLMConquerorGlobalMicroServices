using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.BizCenterApp.Services;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;

namespace MLMConquerorGlobalEdition.BizCenterApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Auth
        builder.Services.AddAuthorizationCore();
        builder.Services.AddScoped<JwtAuthStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<JwtAuthStateProvider>());

        // Shared components (IViewContextService, IThemeService)
        builder.Services.AddSharedComponents();

        // View context initializer (sets BizCenter context from JWT claims)
        builder.Services.AddScoped<ViewContextInitializer>();

        // Auth handler for SharedComponents default HttpClient
        builder.Services.AddScoped<BizCenterAuthHandler>();

        // Default HttpClient — SharedComponents inject this via @inject HttpClient Http.
        // The BizCenterAuthHandler attaches the JWT on every request.
        builder.Services.AddHttpClient("bizcenter", client =>
        {
            // TODO: Replace with config-driven base address before production
            client.BaseAddress = new Uri("https://localhost:7001");
        }).AddHttpMessageHandler(sp => sp.GetRequiredService<BizCenterAuthHandler>());
        builder.Services.AddScoped(sp =>
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("bizcenter"));

        // Typed client for explicit BizCenterApiClient usage
        builder.Services.AddHttpClient<BizCenterApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7001");
        });

        // HTTP client — Signups API (public unauthenticated signup flow)
        builder.Services.AddHttpClient<SignupsApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7147");
        });

        return builder.Build();
    }
}
