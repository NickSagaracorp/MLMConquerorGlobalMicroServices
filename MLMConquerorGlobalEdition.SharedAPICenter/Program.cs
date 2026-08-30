using AspNetCoreRateLimit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedAPICenter.Middleware;
using MLMConquerorGlobalEdition.SharedAPICenter.Services;
using MLMConquerorGlobalEdition.SharedKernel.Server.Behaviors;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Server.Services.CacheService;
using ICurrentUserService   = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICurrentUserService;
using IDateTimeProvider     = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";
});
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

builder.Services.AddControllers();

// AQUÍ NO HAY AUTENTICACIÓN JWT, Y ES A PROPÓSITO.
//
// Había una: AddAuthentication + AddJwtBearer + AddAuthorization, con toda la pinta de estar
// protegiendo este servicio. No protegía nada, y no podía protegerlo aunque alguien lo intentara:
//
//   • CERO ENDPOINTS. Este anfitrión tiene dos controladores y ninguno lleva [Authorize] ni
//     RequireAuthorization(). Los webhooks son públicos por contrato —los llama la pasarela de
//     pago, que no tiene token nuestro— y ExternalController se defiende con una cabecera
//     X-Api-Key comprobada a mano. La tubería entera se ejecutaba en cada petición sin decidir
//     nunca nada.
//
//   • LA LLAVE ERA HMAC MIENTRAS TODO EL SISTEMA FIRMA CON RSA. El emisor único (Authn.JwtService)
//     firma RS256 con la llave privada; aquí se validaba con SymmetricSecurityKey sobre Jwt:Key.
//     Ningún token real habría pasado esa validación jamás.
//
//   • Y ESA Jwt:Key ERA EL LITERAL "YOUR_JWT_KEY_REPLACE_BEFORE_DEPLOY_MIN32CHARS", con un
//     throw que obligaba a que el despliegue tuviera un secreto puesto para un mecanismo que no
//     se usa. La audiencia además estaba mal escrita —"MLMConquerorGlobalEditionClients", sin el
//     punto— frente a la del resto del sistema.
//
// POR QUÉ SE BORRA EN VEZ DE ARREGLARSE. Arreglarlo entero sería poner la validación RSA correcta
// en un anfitrión donde seguiría sin proteger cero endpoints: el mismo adorno, ahora con mejor
// criptografía. Y el adorno es una trampa, no una comodidad: quien mañana ponga un [Authorize]
// aquí lo vería registrado, daría la protección por hecha, y se encontraría con que ningún token
// del sistema valida contra esa llave.
//
// QUÉ PASA SI ALGUIEN AÑADE UN [Authorize] AHORA. Falla en el acto y a la vista: sin middleware de
// autorización, ASP.NET Core lanza al servir el endpoint ("Endpoint contains authorization
// metadata, but a middleware was not found that supports authorization"). Eso es exactamente lo
// que hay que ver — y hay una prueba que lo dice antes, al compilar.
//
// LO QUE HABRÍA QUE HACER ENTONCES: copiar el bloque de SignupAPI o AdminAPI —RsaSecurityKey desde
// Jwt:PublicKeyBase64, emisor y audiencia del resto del sistema, y el evento OnTokenValidated que
// rechaza los retos de 2FA— y volver a poner UseAuthentication/UseAuthorization.

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "MLMConqueror SharedAPICenter",
        Version     = "v1",
        Description = "Inbound payment webhooks and external member data endpoints. " +
                      "Ninguna ruta usa Bearer: los webhooks son públicos por contrato y " +
                      "/api/v1/external se defiende con la cabecera X-Api-Key."
    });

    // SIN DEFINICIÓN DE SEGURIDAD BEARER. La había —con su botón "Authorize" y su requisito global—
    // y era la misma mentira que el bloque de autenticación que se fue de este archivo, pero
    // enseñada al integrador: la documentación decía que estas rutas piden un token cuando ninguna
    // lo mira. Quien viniera a integrarse perdería el tiempo consiguiendo uno.
});

var app = builder.Build();

// Apply pending EF migrations automatically on startup (idempotent).
// Wrapped: a failure here must not terminate the host — the service still
// answers /health and incoming requests once the DB recovers.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "EF migration failed at startup. Service will continue without applying pending migrations.");
    }
}

app.UseMiddleware<DomainExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MLMConqueror SharedAPICenter v1");
    c.RoutePrefix = "swagger";
});
app.UseIpRateLimiting();
// Sin UseAuthentication/UseAuthorization: ver el bloque de arriba. Este anfitrión no protege
// ningún endpoint con token, y dejar la tubería puesta solo serviría para aparentar que sí.
app.MapControllers();

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    var status = canConnect ? "Healthy" : "Unhealthy";
    return Results.Ok(new
    {
        service   = "MLMConquerorGlobalEdition.SharedAPICenter",
        status,
        checks    = new { database = canConnect ? "Healthy" : "Unhealthy" },
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

app.Run();
