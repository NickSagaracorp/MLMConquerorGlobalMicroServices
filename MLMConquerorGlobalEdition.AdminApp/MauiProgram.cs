using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.AdminApp.Services;
using MLMConquerorGlobalEdition.ClientCore;
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

        // ── LA PUERTA ────────────────────────────────────────────────────────────────────────
        // Esta aplicación NO tiene login propio. Entra por SignupAPI, que es la única puerta de
        // la solución y la única que sabe de segundo factor. Hasta ahora posteaba credenciales a
        // AdminAPI, que las comprobaba y devolvía un token con los roles dentro sin preguntar por
        // el segundo factor: era una puerta paralela que abría el panel entero con solo la
        // contraseña. Ese endpoint ya no existe.
        //
        // El cliente HTTP y su UseCookies=false los pone AddAuthApiClient, que vive en ClientCore
        // para que los cuatro anfitriones —dos portales y dos MAUI— monten exactamente el mismo.
        // Sin esa opción, el manejador se tragaría el Set-Cookie del refresh token y la sesión
        // moriría al caducar el JWT.
        //
        // TODO: dirección de SignupAPI desde configuración antes de producción, igual que las de
        // AdminAPI de más abajo.
        builder.Services.AddAuthApiClient("https://localhost:7005");
        builder.Services.AddScoped<IAccessTokenProvider, SecureStorageAccessTokenProvider>();
        builder.Services.AddScoped<AuthApiGateway>();

        // El reto del segundo factor mientras se resuelve. Singleton porque tiene que sobrevivir a
        // la navegación entre la pantalla de entrada y la del código, que son dos componentes.
        builder.Services.AddSingleton<AdminLoginChallenge>();

        // Shared components (IViewContextService, IThemeService)
        builder.Services.AddSharedComponents();

        // Admin-specific view context initializer (manages impersonation state)
        builder.Services.AddScoped<AdminViewContextInitializer>();

        // Auth handler for SharedComponents default HttpClient
        builder.Services.AddScoped<AdminAuthHandler>();

        // Default HttpClient — SharedComponents inject this via @inject HttpClient Http.
        // The AdminAuthHandler attaches the effective JWT (admin or impersonation) on every request.
        builder.Services.AddHttpClient("admin", client =>
        {
            // TODO: Replace with config-driven base address before production
            client.BaseAddress = new Uri("https://localhost:7002");
        }).AddHttpMessageHandler(sp => sp.GetRequiredService<AdminAuthHandler>());
        builder.Services.AddScoped(sp =>
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("admin"));

        // Typed client for explicit AdminApiClient usage
        builder.Services.AddHttpClient<AdminApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7002");
        });

        return builder.Build();
    }
}
