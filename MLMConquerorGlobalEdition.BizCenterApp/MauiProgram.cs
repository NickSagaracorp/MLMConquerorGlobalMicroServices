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

        // A DÓNDE MANDAR A QUIEN NO TIENE CUENTA. Aquí vivía SignupsApiClient, que solo usaba la
        // pantalla de alta de esta aplicación. Esa pantalla era una copia atrasada del asistente de
        // verdad —mandaba SponsorMemberId, un campo que AmbassadorSignupRequest no tiene, así que el
        // deserializador lo tiraba y el alta se guardaba SIN PATROCINADOR— y se ha borrado: desde el
        // centro de negocios no se hace ningún alta, solo desde la aplicación de alta.
        //
        // Lo que queda es el ENLACE, y sale de configuración —con un valor por defecto para el
        // entorno de desarrollo— en vez de escrito dentro de la pantalla de login. La aplicación de
        // alta cambia de dirección en cada entorno; una pantalla no es sitio para saberlo.
        builder.Services.AddSingleton(new AppLinks
        {
            SignupAppUrl = builder.Configuration["SignupAppUrl"]
                        ?? "https://localhost:7147/ambassador-join"
        });

        return builder.Build();
    }
}
