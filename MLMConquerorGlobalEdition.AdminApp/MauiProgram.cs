using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.AdminApp.Services;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;

namespace MLMConquerorGlobalEdition.AdminApp;

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
        builder.Services.AddScoped<AdminJwtAuthStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<AdminJwtAuthStateProvider>());

        // Shared components (IViewContextService, IThemeService)
        builder.Services.AddSharedComponents();

        // Admin-specific view context initializer (manages impersonation state)
        builder.Services.AddScoped<AdminViewContextInitializer>();

        // HTTP client — base address to AdminAPI
        // TODO: Replace with config-driven base address before production
        builder.Services.AddHttpClient<AdminApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7002");
        });

        return builder.Build();
    }
}
