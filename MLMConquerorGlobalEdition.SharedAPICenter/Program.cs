using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;
using AspNetCoreRateLimit;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedAPICenter.Middleware;
using MLMConquerorGlobalEdition.SharedAPICenter.Seguridad;
using MLMConquerorGlobalEdition.SharedAPICenter.Services;
using MLMConquerorGlobalEdition.SharedKernel.Server.Behaviors;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Server.Services.CacheService;
using ICurrentUserService   = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICurrentUserService;
using IDateTimeProvider     = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;

var builder = WebApplication.CreateBuilder(args);
// Secretos de AWS Secrets Manager (lista SecretosAws:Secretos de este servicio). Va antes que
// cualquier cosa que lea la configuracion: si falta uno, el servicio no arranca.
builder.Configuration.AddSecretosDeAws();

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

// AUTENTICACIÓN SIN JWT, A PROPÓSITO. Ningún llamante de este servicio tiene un token nuestro:
//   • /api/v1/external lo llaman sistemas externos con la cabecera X-Api-Key (ApiKeyExternaHandler).
//   • /api/v1/webhooks lo llaman las pasarelas, que firman el cuerpo con HMAC-SHA256 y el secreto
//     de cada una, Webhooks:{provider}:Secreto (FirmaWebhookHandler).
// Los dos esquemas fallan cerrados: sin secreto configurado no entra nadie. No hay esquema por
// defecto: cada controlador declara el suyo en su [Authorize].
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyExternaHandler>(ApiKeyExternaHandler.Esquema, null)
    .AddScheme<AuthenticationSchemeOptions, FirmaWebhookHandler>(FirmaWebhookHandler.Esquema, null);
builder.Services.AddAuthorization();

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
                      "Ninguna ruta usa Bearer: los webhooks se firman con HMAC-SHA256 " +
                      "(X-Webhook-Timestamp y X-Webhook-Signature) y /api/v1/external pide la " +
                      "cabecera X-Api-Key."
    });

    // Sin definición de seguridad Bearer: ninguna ruta de este servicio pide un token.
});

// Detras del balanceador: IP y esquema reales del cliente.
builder.Services.AddProxyForwardedHeaders(builder.Configuration);

var app = builder.Build();

// Primero de todo: lo que sigue (HSTS, redireccion, limite por IP, auth) ve al cliente real.
app.UseForwardedHeaders();

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
app.UseAuthentication();
app.UseAuthorization();
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
