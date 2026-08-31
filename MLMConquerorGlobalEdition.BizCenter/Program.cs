using MLMConquerorGlobalEdition.SharedKernel.Server.Middleware;
using System.Security.Cryptography;
using AspNetCoreRateLimit;
using Hangfire;
using Hangfire.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Hangfire.InMemory;
using MLMConquerorGlobalEdition.BizCenter.Infrastructure;
using MLMConquerorGlobalEdition.BizCenter.Jobs;
using MLMConquerorGlobalEdition.BizCenter.Middleware;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.Repository.Seeders;
using FluentValidation;
using FluentValidation.AspNetCore;
using MLMConquerorGlobalEdition.SharedKernel.Server.Behaviors;
using MLMConquerorGlobalEdition.SharedKernel.Server.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Logging;
using ICacheService             = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService  = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService              = MLMConquerorGlobalEdition.SharedKernel.Server.Services.CacheService;
using IErrorTrackingService     = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;

using MLMConquerorGlobalEdition.Repository.Services.Payout;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddPiiMaskingConsole();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity is needed here because the BizCenter Profile page lets the member
// change their own email and password (handled in-process via UserManager,
// audit-logged to MemberCredentialChangeLogs). Mirrors the AdminAPI setup.
builder.Services.AddIdentityCore<MLMConquerorGlobalEdition.Repository.Identity.ApplicationUser>(options =>
{
    options.Password.RequiredLength         = 8;
    options.Password.RequireDigit           = true;
    options.Password.RequireUppercase       = true;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail         = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Aquí no sale ningún correo con enlace, pero la Identity es la misma que la de SignupAPI: dejar
// este anfitrión con el día entero por defecto sería tener dos vigencias distintas para el mismo
// proveedor de tokens. Ver EmailLinkLifetime.
builder.Services.AddEmailLinkTokenLifetime(builder.Configuration);

// Order matters: Validation runs first, then error handling wraps the handler.
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});

// Auto-register all FluentValidation validators in this assembly
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
// Run DTO-level validators automatically on every model-binding (defense-in-depth
// against injection / oversize payloads BEFORE handlers execute).
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
// La comprobación de propiedad de las rutas que llevan un {memberId} ajeno en la URL. Ver
// DownlineGuard: la regla sigue siendo CallerIdentity.CanActOnMember, con la descendencia del que
// llama como sujeto añadido para las pantallas que recorren el árbol hacia abajo.
builder.Services.AddScoped<IDownlineGuard, DownlineGuard>();
// Registers all rank services in one call:
//   IEnrollmentTeamPointsService, IPersonalCustomerPointsService,
//   IRankQualificationService, IRankComputationService.
// EnrollmentTeamService now depends on IEnrollmentTeamPointsService so this
// call must precede the EnrollmentTeamService registration.
builder.Services.AddRankServices();
MLMConquerorGlobalEdition.Repository.Services.Teams.PlacementServicesRegistration.AddPlacementServices(builder.Services);
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTreeNodeService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.DualTreeNodeService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IEnrollmentTeamService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.EnrollmentTeamService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.DualTeamService>();

// Sprint-15 Bug C: shared dual-team leg-points recalculator (also injected
// by SignupAPI). Both placement handlers route through this so leg points stay
// consistent regardless of which service did the placement.
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Trees.IDualTeamPointsRecalculator,
                            MLMConquerorGlobalEdition.Repository.Services.Trees.DualTeamPointsRecalculator>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.ICommissionsService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.CommissionsService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Wallets.IMemberWalletService,
                            MLMConquerorGlobalEdition.Repository.Services.Wallets.MemberWalletService>();

// MemberWalletService da de alta la cuenta en el proveedor cuando el miembro elige o cambia
// su método de cobro, así que necesita los clientes de gateway. Se registran los clientes
// SOLOS (no el pipeline completo de payouts): BizCenter no orquesta pagos ni emite recibos.
builder.Services.AddPayoutGatewayClients();
builder.Services.AddScoped<IS3PresignedUrlService, S3PresignedUrlService>();

// Profile photo upload uses the same S3 bucket the Signup wizard uses for
// checkout screenshots. The credentials lookup mirrors SignupAPI's wiring.
builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(_ =>
{
    var accessKey = builder.Configuration["AWS:Credentials:AccessKey"];
    var secretKey = builder.Configuration["AWS:Credentials:SecretKey"];
    var region    = builder.Configuration["AWS:S3:Region"] ?? "us-east-1";
    if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        return new Amazon.S3.AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));
    return new Amazon.S3.AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
});
builder.Services.AddScoped<IS3FileService, S3FileService>();

builder.Services.AddDataProtection();
builder.Services.AddScoped<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEncryptionService, EncryptionService>();
// La tokenización de tarjeta ya no es de este proyecto: vive en SharedKernel, que es el único sitio
// que alcanzan a la vez BizCenter, SignupAPI y la aplicación de alta. Aquí sigue registrándose la
// implementación simulada porque este alta de tarjeta SÍ recibe el número en el servidor —viene por
// el cuerpo de AddCreditCardCommand—, cosa que el alta de miembro no hace y no debe hacer.
builder.Services.AddScoped<MLMConquerorGlobalEdition.SharedKernel.Billing.ICardTokenizationService,
                            MLMConquerorGlobalEdition.SharedKernel.Billing.SimulatedCardTokenizationService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddSingleton<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider>(
    sp => sp.GetRequiredService<IDateTimeProvider>());
builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();

// Cache backend — probe Redis at startup. Cache:Mode controls behavior on
// failure: "Required" (production) → throw, refuse to start with memory
// cache that wouldn't be safe across multiple instances. "Optional" (dev)
// → fall back to in-process memory cache so dev keeps working without
// Redis. CacheBackendInfo is a singleton the /health/cache endpoint reads.
{
    var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var mode      = (builder.Configuration["Cache:Mode"] ?? "Optional").Trim();
    var required  = mode.Equals("Required", StringComparison.OrdinalIgnoreCase);

    var redisReachable = false;
    try
    {
        using var probe = StackExchange.Redis.ConnectionMultiplexer.Connect(
            new StackExchange.Redis.ConfigurationOptions
            {
                EndPoints          = { redisConn },
                ConnectTimeout     = 250,
                AbortOnConnectFail = false
            });
        redisReachable = probe.IsConnected;
    }
    catch { redisReachable = false; }

    MLMConquerorGlobalEdition.SharedKernel.Services.CacheBackendInfo info;
    if (redisReachable)
    {
        builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
        info = new MLMConquerorGlobalEdition.SharedKernel.Services.CacheBackendInfo
        {
            Backend        = "Redis",
            ConnectionHint = redisConn,
            Mode           = required ? "Required" : "Optional"
        };
        Console.WriteLine($"[Cache] Redis reachable at {redisConn} — distributed cache enabled (mode={info.Mode}).");
    }
    else if (required)
    {
        throw new InvalidOperationException(
            $"[Cache] Cache:Mode is 'Required' but Redis at '{redisConn}' is unreachable. " +
            "Refusing to start with in-process memory cache.");
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();
        info = new MLMConquerorGlobalEdition.SharedKernel.Services.CacheBackendInfo
        {
            Backend        = "Memory",
            ConnectionHint = "in-process",
            Mode           = "Optional"
        };
        Console.WriteLine($"[Cache] Redis unreachable at {redisConn} — falling back to in-process memory cache (mode=Optional).");
    }
    builder.Services.AddSingleton(info);
}
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

builder.Services.AddScoped<MemberStatisticSnapshotJob>();
builder.Services.AddScoped<ExpiredTokenCleanupJob>();
builder.Services.AddScoped<LoyaltyPointsMonthlyRollupJob>();
builder.Services.AddScoped<AutoPlacementJob>();
builder.Services.AddHangfire(cfg =>
{
    cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
       .UseSimpleAssemblyNameTypeSerializer()
       .UseRecommendedSerializerSettings();

    if (builder.Environment.IsDevelopment())
        cfg.UseInMemoryStorage();
    else
        cfg.UseSqlServerStorage(
            builder.Configuration.GetConnectionString("HangFire")
            ?? builder.Configuration.GetConnectionString("DefaultConnection"));
});
// Restrict this Hangfire server to its own queue so it does not pick up
// jobs whose types live in assemblies this service does not reference.
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", 5);
    options.Queues = new[] { "bizcenter" };
});

builder.Services.AddControllers();
builder.Services.AddHttpClient("certificates");

// Typed HttpClient for RankEngine on-demand calls (currently just the member
// self-service certificate-generation endpoint). The BaseAddress falls back to
// the canonical dev port (7009) so local environments work without extra config.
{
    var rankEngineBaseUrl = builder.Configuration["Services:RankEngineBaseUrl"]
                            ?? "https://localhost:7009/";
    builder.Services.AddHttpClient("rankengine", c =>
    {
        c.BaseAddress = new Uri(rankEngineBaseUrl);
        c.Timeout     = TimeSpan.FromSeconds(15);
    });
}
builder.Services.AddScoped<IRankEngineClient, RankEngineClient>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? (builder.Environment.IsDevelopment()
        ? new[] { "https://localhost:7002", "https://localhost:7004" }
        : Array.Empty<string>());

builder.Services.AddCors(options =>
{
    options.AddPolicy("BizCenterPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                  .AllowCredentials();
    });
});

var publicKeyBase64 = JwtKeyGuard.ValidatePublicKey(builder.Configuration["Jwt:PublicKeyBase64"]);

var rsaValidation = RSA.Create();
rsaValidation.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
var jwtValidationKey = new RsaSecurityKey(rsaValidation);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = jwtValidationKey,
            ClockSkew                = TimeSpan.Zero
        };

        // Segundo cinturón, detrás de la audiencia: un token que lleve el claim de propósito es
        // un reto de 2FA sin verificar y no autoriza nada. Ver ChallengeAudience.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                if (ChallengeAudience.CarriesPurpose(ctx.Principal!.Claims))
                    ctx.Fail("Un reto de 2FA no autoriza: falta completar el segundo factor.");
                return Task.CompletedTask;
            }
        };
    });

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MLMConqueror BizCenter API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "Bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Apply pending EF migrations and seed baseline data on startup (idempotent).
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.MigrateAsync();
    await RankGateSeeder.SeedAsync(db, logger);
}

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseMiddleware<DomainExceptionMiddleware>();

// Serve uploaded ticket attachments from wwwroot/uploads. The download URL is built
// in GetTicketHandler (origin + FileUrl) and rendered by the SharedComponents
// TicketDetailPage as a regular <a href>.
app.UseStaticFiles();

app.UseCors("BizCenterPolicy");

app.UseSwagger();
app.UseSwaggerUI();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();

// Un token de suplantacion marcado de solo lectura no escribe. Va detras de la autorizacion para
// que un 401 o un 403 por rol se contesten antes, y delante de las rutas para que ninguna llegue a
// ejecutarse. Ver ImpersonationScope.
app.UseImpersonationReadOnly();

app.MapControllers();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthorizationFilter() },
    AppPath       = "/health"
});

RecurringJob.AddOrUpdate<MemberStatisticSnapshotJob>(
    "member-statistic-snapshot",
    job => job.ExecuteAsync(),
    "0 1 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<ExpiredTokenCleanupJob>(
    "expired-token-cleanup",
    job => job.ExecuteAsync(),
    "0 5 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<LoyaltyPointsMonthlyRollupJob>(
    "loyalty-points-monthly-rollup",
    job => job.ExecuteAsync(),
    "30 2 1 * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<AutoPlacementJob>(
    "auto-placement",
    job => job.ExecuteAsync(),
    "0 */6 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    var status = canConnect ? "Healthy" : "Unhealthy";
    return Results.Ok(new
    {
        service   = "MLMConquerorGlobalEdition.BizCenter",
        status,
        checks    = new { database = canConnect ? "Healthy" : "Unhealthy" },
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

// Cache backend introspection — operations team should monitor this. If
// "backend":"Memory" shows up in production, Redis is down and the API is
// running on per-instance fallback (NOT safe for multi-instance deploys).
app.MapGet("/health/cache", (MLMConquerorGlobalEdition.SharedKernel.Services.CacheBackendInfo info) =>
    Results.Ok(new
    {
        service        = "MLMConquerorGlobalEdition.BizCenter",
        backend        = info.Backend,
        connectionHint = info.ConnectionHint,
        mode           = info.Mode,
        memoryFallback = info.IsMemoryFallback,
        timestamp      = DateTime.UtcNow
    })).AllowAnonymous();

app.Run();
