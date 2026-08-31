using MLMConquerorGlobalEdition.SharedKernel.Server.Middleware;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Amazon.S3;
using AspNetCoreRateLimit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedKernel.Server.Behaviors;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Server.Services.CacheService;
using MLMConquerorGlobalEdition.SharedKernel.Logging;
using MLMConquerorGlobalEdition.Notifications;
using MLMConquerorGlobalEdition.Authn;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;
using MLMConquerorGlobalEdition.Repository.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Middleware;
using MLMConquerorGlobalEdition.SignupAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddPiiMaskingConsole();

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Identity
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
.AddDefaultTokenProviders()
.AddSignInManager();

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});

// FluentValidation — register all *Validator types in this assembly so that
// (a) MediatR command validators run inside the pipeline, AND
// (b) DTO-level validators run during model binding via AddFluentValidationAutoValidation,
//     giving us defense-in-depth (regex / length / whitelist) before any handler executes.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();


// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

// AWS S3
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var accessKey = builder.Configuration["AWS:Credentials:AccessKey"];
    var secretKey = builder.Configuration["AWS:Credentials:SecretKey"];
    var region    = builder.Configuration["AWS:S3:Region"] ?? "us-east-1";

    if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        return new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));

    return new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
});
builder.Services.AddScoped<IS3FileService, S3FileService>();

// Sprint-15 Bug C: shared dual-team leg-points recalculator. Both SignupAPI and
// BizCenter placement handlers depend on this so a placement created by either
// side leaves LeftLegPoints / RightLegPoints + MemberStatistics.DualTeamPoints
// consistent up the binary tree.
builder.Services.AddScoped<
    MLMConquerorGlobalEdition.Repository.Services.Trees.IDualTeamPointsRecalculator,
    MLMConquerorGlobalEdition.Repository.Services.Trees.DualTeamPointsRecalculator>();

builder.Services.AddScoped<ISponsorBonusService, SponsorBonusService>();
builder.Services.AddScoped<IFastStartBonusService, FastStartBonusService>();
// Activación del alta: cierre del pedido, alta del miembro, deltas del upline y comisiones.
// Lo usan las dos vías que cobran — la inmediata (tarjeta, token, descuento) al completar, y la
// de cripto al confirmar el cobro a mano — para que no existan dos versiones de lo mismo.
builder.Services.AddScoped<ISignupActivationService, SignupActivationService>();
builder.Services.AddScoped<ITokenRedemptionService, TokenRedemptionService>();
builder.Services.AddScoped<IFraudFingerprintService, FraudFingerprintService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();

// Recurring billing — enrolment service creates the SubscriptionBillingState
// record as part of the CompleteSignup flow so the dunning sweep can pick it up.
// The Billing assembly defines its own IDateTimeProvider (distinct from SharedKernel's)
// so register it here alongside the service.
builder.Services.AddSingleton<MLMConquerorGlobalEdition.Billing.Services.IDateTimeProvider,
                               MLMConquerorGlobalEdition.Billing.Services.DateTimeProvider>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Billing.Services.Recurring.IRecurringBillingEnrollmentService,
                            MLMConquerorGlobalEdition.Billing.Services.Recurring.RecurringBillingEnrollmentService>();

// JWT Service
builder.Services.AddAuthnAccessTokens();

// Librería Authn: challenge firmado, enrolamiento TOTP y la orquestación de los tres
// canales de 2FA. Todo el camino de dos factores —login, verificación, reenvío y
// enrolamiento— pasa por aquí; el servicio local que hacía esto ya no existe.
builder.Services.AddAuthnChallengeTokens();
builder.Services.AddAuthnTotpEnrollment();
builder.Services.AddAuthnTwoFactor();

// Email/SMS transport — provider selected by Notifications:Email:Provider /
// Notifications:Sms:Provider config. Defaults to Null (log-only) when unset.
// The 2FA login flow needs IEmailService registered so the LoginHandler can
// request a "TWO_FACTOR_CODE" email.
builder.Services.AddNotifications(builder.Configuration);

// Error Tracking
builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();

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

// Firebase push notifications
builder.Services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

// HangFire
builder.Services.AddScoped<ProcessScheduledCancellationsJob>();
builder.Services.AddScoped<BuilderBonusSweepJob>();
builder.Services.AddScoped<FastStartBonusSweepJob>();
builder.Services.AddScoped<ContestPointsSweepJob>();
builder.Services.AddScoped<ApplyMemberStatisticDeltasJob>();
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        builder.Configuration.GetConnectionString("HangFire")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")));
// Restrict this Hangfire server to its own queue so it does not pick up
// jobs whose types live in assemblies this service does not reference.
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", 5);
    options.Queues = new[] { "signups" };
});

// Controllers only — no Blazor
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Return ApiResponse<T> on model-binding failures instead of ValidationProblemDetails
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToArray();
        var response = MLMConquerorGlobalEdition.SharedKernel.ApiResponse<object>
            .Fail("VALIDATION_ERROR", string.Join("; ", errors));
        response.Errors = errors;
        return new BadRequestObjectResult(response);
    };
});

// CORS — allow Signups frontend origin
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignupsFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["https://localhost:7147"];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// La privada se valida aquí, al arrancar, aunque quien la use sea JwtService.
// Esos servicios son Scoped, así que su constructor —y con él el guarda— no correría
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

// Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MLMConqueror Signup API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed roles and root ambassador on startup
using (var scope = app.Services.CreateScope())
{
    var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var config      = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var seedLogger  = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await db.Database.MigrateAsync();
    await RolesSeeder.SeedAsync(roleManager, seedLogger);
    await RootAmbassadorSeeder.SeedAsync(db, userManager, config, seedLogger);
    await CountriesSeeder.SeedAsync(db, seedLogger);
    await ProductsSeeder.SeedAsync(db, seedLogger);
    await CountryProductDefaultSeeder.SeedAsync(db, seedLogger);
}

app.UseStaticFiles();
app.UseMiddleware<DomainExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseIpRateLimiting();
app.UseCors("SignupsFrontend");
app.UseAuthentication();
app.UseAuthorization();

// Un token de suplantacion marcado de solo lectura no escribe. Va detras de la autorizacion para
// que un 401 o un 403 por rol se contesten antes, y delante de las rutas para que ninguna llegue a
// ejecutarse. Ver ImpersonationScope.
app.UseImpersonationReadOnly();

app.MapControllers();

// HangFire
app.UseHangfireDashboard("/hangfire");
RecurringJob.AddOrUpdate<ProcessScheduledCancellationsJob>(
    "process-scheduled-cancellations",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 1 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "signups" });

RecurringJob.AddOrUpdate<FastStartBonusSweepJob>(
    "fsb-sweep",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/5 * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "signups" });

// QueueName must be set explicitly. Without it Hangfire stamps the
// recurring-job entry with Queue=default — and SignupAPI's Hangfire server
// only listens on "signups" — so the cron tick fires but the enqueued job
// sits forever in a queue no worker picks up. Setting QueueName here
// pins both the recurring entry AND the enqueued job to "signups", which
// SignupAPI's worker is configured to drain.
RecurringJob.AddOrUpdate<BuilderBonusSweepJob>(
    "builder-bonus-sweep",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/10 * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "signups" });

RecurringJob.AddOrUpdate<ContestPointsSweepJob>(
    "contest-points-sweep",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/10 * * * *",                    // every 10 minutes
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "signups" });

// Sprint-16 — drains MemberStatisticDeltas into MemberStatistics. Phase-3 of
// CompleteSignup enqueues 1 delta per ancestor (76 per signup at the deep end
// of the tree) in a single batch insert; this job groups by upline + applies
// the summed deltas once per cycle. 1-min cadence: stat staleness < 60s,
// well below the 5-min rank evaluation cycle and the nightly snapshot job.
RecurringJob.AddOrUpdate<ApplyMemberStatisticDeltasJob>(
    "apply-member-statistic-deltas",
    job => job.ExecuteAsync(CancellationToken.None),
    "* * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "signups" });

// Dev-only: drain the MemberStatisticDelta queue on-demand, in SignupAPI's own process so
// the job type resolves (the shared Hangfire recurring scheduler on sibling services cannot
// load this assembly and poisons the recurring entry — see apply-member-statistic-deltas).
// Runs the same job code, draining until empty.
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/v1/dev/drain-deltas",
        async (MLMConquerorGlobalEdition.Repository.Jobs.ApplyMemberStatisticDeltasJob job, CancellationToken ct) =>
        {
            await job.ExecuteAsync(ct);
            return Results.Ok(new { drained = true });
        }).AllowAnonymous();
}

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    var status = canConnect ? "Healthy" : "Unhealthy";
    return Results.Ok(new
    {
        service   = "MLMConquerorGlobalEdition.SignupAPI",
        status,
        checks    = new { database = canConnect ? "Healthy" : "Unhealthy" },
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

// Cache backend introspection — see notes on BizCenter API. "Memory" in
// production means Redis is down and the signup join page is recomputing
// countries/products/membership-levels from DB on every request.
app.MapGet("/health/cache", (MLMConquerorGlobalEdition.SharedKernel.Services.CacheBackendInfo info) =>
    Results.Ok(new
    {
        service        = "MLMConquerorGlobalEdition.SignupAPI",
        backend        = info.Backend,
        connectionHint = info.ConnectionHint,
        mode           = info.Mode,
        memoryFallback = info.IsMemoryFallback,
        timestamp      = DateTime.UtcNow
    })).AllowAnonymous();

app.Run();
