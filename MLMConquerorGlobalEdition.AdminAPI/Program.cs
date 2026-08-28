using System.Security.Cryptography;
using AspNetCoreRateLimit;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.AdminAPI.Middleware;
using MLMConquerorGlobalEdition.AdminAPI.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using MLMConquerorGlobalEdition.SharedKernel.Behaviors;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Logging;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.Billing.Extensions;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using ICacheService         = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;
using ICurrentUserService   = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICurrentUserService;
using IDateTimeProvider     = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;
using CacheService          = MLMConquerorGlobalEdition.SharedKernel.Services.CacheService;
using JwtService            = MLMConquerorGlobalEdition.AdminAPI.Services.JwtService;
using MLMConquerorGlobalEdition.Notifications;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddPiiMaskingConsole();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequiredLength         = 8;
    options.Password.RequireDigit           = true;
    options.Password.RequireUppercase       = true;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail         = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
// Run DTO-level validators automatically on every model-binding (defense-in-depth
// against injection / oversize payloads BEFORE handlers execute).
builder.Services.AddFluentValidationAutoValidation();


builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddRankServices();
MLMConquerorGlobalEdition.Repository.Services.Teams.PlacementServicesRegistration.AddPlacementServices(builder.Services);
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTreeNodeService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.DualTreeNodeService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.DualTeamService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IEnrollmentTeamService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.EnrollmentTeamService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.ICommissionsService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.CommissionsService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Wallets.IMemberWalletService,
                            MLMConquerorGlobalEdition.Repository.Services.Wallets.MemberWalletService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();

// Sprint-15 follow-up: shared dual-team leg-points recalculator. AdminAPI's
// AdminPlaceMember / AdminRemovePlacement handlers previously had local broken
// implementations (counted nodes instead of summing PersonalPoints). They now
// delegate to the single source of truth shared with SignupAPI + BizCenter.
builder.Services.AddScoped<
    MLMConquerorGlobalEdition.Repository.Services.Trees.IDualTeamPointsRecalculator,
    MLMConquerorGlobalEdition.Repository.Services.Trees.DualTeamPointsRecalculator>();

// ── Billing routing + high-volume planner (preview endpoint) ─────────────
// Note: Billing services bind to the sibling `MLMConquerorGlobalEdition.Billing.Services.IDateTimeProvider`
// and `MLMConquerorGlobalEdition.Billing.Services.ICurrentUserService` (separate types from SharedKernel's —
// same simple names, different namespaces), so both must be registered alongside the SharedKernel variants.
// PayoutOrchestrator (and ChargeHandler) take the Billing-local types; AdminAPI's own handlers take SharedKernel's.
builder.Services.AddSingleton<MLMConquerorGlobalEdition.Billing.Services.IDateTimeProvider,
                               MLMConquerorGlobalEdition.Billing.Services.DateTimeProvider>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Billing.Services.ICurrentUserService,
                            MLMConquerorGlobalEdition.Billing.Services.CurrentUserService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Billing.Services.Routing.ICurrencyConversionService,
                            MLMConquerorGlobalEdition.Billing.Services.Routing.CurrencyConversionService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Billing.Services.Routing.ICardBrandDetector,
                            MLMConquerorGlobalEdition.Billing.Services.Routing.CardBrandDetector>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Billing.Services.Routing.IGatewaySplitSelector,
                            MLMConquerorGlobalEdition.Billing.Services.Routing.GatewaySplitSelector>();
builder.Services.AddScoped<IGatewayRouter,
                            MLMConquerorGlobalEdition.Billing.Services.Routing.GatewayRouter>();
builder.Services.AddScoped<IRecurringBillingPlanner, RecurringBillingPlanner>();

// ── Payout gateway abstraction + orchestrator ─────────────────────────────
builder.Services.AddPayoutServices();

builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IS3StorageService, S3StorageService>();

// Cache backend — probe Redis at startup. Cache:Mode controls behavior on
// failure: "Required" (production) → throw. "Optional" (dev) → fall back
// to in-process memory cache. CacheBackendInfo is a singleton the
// /health/cache endpoint reads.
{
    var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var mode      = (builder.Configuration["Cache:Mode"] ?? "Optional").Trim();
    var required  = mode.Equals("Required", StringComparison.OrdinalIgnoreCase);

    var redisReachable = false;
    StackExchange.Redis.IConnectionMultiplexer? multiplexer = null;
    try
    {
        multiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(
            new StackExchange.Redis.ConfigurationOptions
            {
                EndPoints          = { redisConn },
                ConnectTimeout     = 250,
                AbortOnConnectFail = false
            });
        redisReachable = multiplexer.IsConnected;
    }
    catch { redisReachable = false; }

    MLMConquerorGlobalEdition.SharedKernel.Services.CacheBackendInfo info;
    if (redisReachable)
    {
        // CacheService.IncrementAsync necesita una conexión para INCR: sin contador atómico
        // los topes de 2FA (5 intentos por challenge, 3 emisiones por ventana) dejan de ser
        // topes en cuanto hay concurrencia.
        //
        // Se abre una conexión propia en vez de reutilizar la del sondeo. Aquella lleva
        // ConnectTimeout=250 para no retrasar el arranque cuando Redis no está, y ese valor
        // es demasiado agresivo para una conexión de larga vida: bajo carga o con latencia
        // de red, una reconexión se quedaría corta y los contadores caerían al respaldo por
        // proceso más a menudo de lo debido, justo cuando más tráfico hay.
        multiplexer!.Dispose();
        builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
            StackExchange.Redis.ConnectionMultiplexer.Connect(
                new StackExchange.Redis.ConfigurationOptions
                {
                    EndPoints          = { redisConn },
                    ConnectTimeout     = 5000,
                    ConnectRetry       = 3,
                    AbortOnConnectFail = false
                }));
        builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
        info = new MLMConquerorGlobalEdition.SharedKernel.Services.CacheBackendInfo
        {
            Backend = "Redis", ConnectionHint = redisConn,
            Mode = required ? "Required" : "Optional"
        };
        Console.WriteLine($"[Cache] Redis reachable at {redisConn} — distributed cache enabled (mode={info.Mode}).");
    }
    else if (required)
    {
        multiplexer?.Dispose();
        throw new InvalidOperationException(
            $"[Cache] Cache:Mode is 'Required' but Redis at '{redisConn}' is unreachable. " +
            "Refusing to start with in-process memory cache.");
    }
    else
    {
        multiplexer?.Dispose();
        builder.Services.AddDistributedMemoryCache();
        info = new MLMConquerorGlobalEdition.SharedKernel.Services.CacheBackendInfo
        {
            Backend = "Memory", ConnectionHint = "in-process", Mode = "Optional"
        };
        Console.WriteLine($"[Cache] Redis unreachable at {redisConn} — falling back to in-process memory cache (mode=Optional).");
    }
    builder.Services.AddSingleton(info);
}
builder.Services.AddSingleton<ICacheService, CacheService>();

// Email/SMS transport — provider selected by Notifications:Email:Provider /
// Notifications:Sms:Provider config. Defaults to Null (log-only) when unset.
builder.Services.AddNotifications(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? (builder.Environment.IsDevelopment()
        ? new[] { "https://localhost:7001", "https://localhost:7003" }
        : Array.Empty<string>());

builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminApiPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                  .AllowCredentials();
    });
});

// La privada se valida aquí, al arrancar, aunque quien la use sea JwtService.
// Ese servicio es Scoped, así que su constructor —y con él el guarda— no correría
// hasta la primera petición que firme un token: el servicio arrancaría con una llave
// revocada o ausente y solo fallaría cuando alguien intentara iniciar sesión.
JwtKeyGuard.ValidatePrivateKey(builder.Configuration["Jwt:PrivateKeyBase64"]);

var publicKeyBase64 = JwtKeyGuard.ValidatePublicKey(builder.Configuration["Jwt:PublicKeyBase64"]);

var rsaValidation = RSA.Create();
rsaValidation.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
var jwtValidationKey = new RsaSecurityKey(rsaValidation);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.UseSecurityTokenValidators = true;
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
    });

builder.Services.AddAuthorization();

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("HealthCheck")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    });
builder.Services.AddHttpClient("SignupApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:SignupAPI"] ?? "https://localhost:7005");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MLMConqueror AdminAPI", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT token."
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
    await CompanyInfoSeeder.SeedAsync(db, logger);
    await CountryProductDefaultSeeder.SeedAsync(db, logger);
    await GatewayRoutingSeeder.SeedAsync(db, logger);
    await RecurringBillingSeeder.SeedAsync(db, logger);
    await RankGateSeeder.SeedAsync(db, logger);
}

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<DomainExceptionMiddleware>();
app.UseCors("AdminApiPolicy");

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();

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
        service   = "MLMConquerorGlobalEdition.AdminAPI",
        status,
        checks    = new { database = canConnect ? "Healthy" : "Unhealthy" },
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

// Cache backend introspection — see notes on BizCenter API. "Memory" in
// production means Redis is down and admins are working on stale data.
app.MapGet("/health/cache", (MLMConquerorGlobalEdition.SharedKernel.Services.CacheBackendInfo info) =>
    Results.Ok(new
    {
        service        = "MLMConquerorGlobalEdition.AdminAPI",
        backend        = info.Backend,
        connectionHint = info.ConnectionHint,
        mode           = info.Mode,
        memoryFallback = info.IsMemoryFallback,
        timestamp      = DateTime.UtcNow
    })).AllowAnonymous();

app.Run();
