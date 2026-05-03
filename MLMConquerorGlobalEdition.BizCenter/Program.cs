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
using FluentValidation;
using MLMConquerorGlobalEdition.SharedKernel.Behaviors;
using MLMConquerorGlobalEdition.SharedKernel.Logging;
using ICacheService             = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService  = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService              = MLMConquerorGlobalEdition.SharedKernel.Services.CacheService;
using IErrorTrackingService     = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;

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

// Order matters: Validation runs first, then error handling wraps the handler.
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});

// Auto-register all FluentValidation validators in this assembly
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Ranks.IRankComputationService,
                            MLMConquerorGlobalEdition.Repository.Services.Ranks.RankComputationService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTreeNodeService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.DualTreeNodeService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IEnrollmentTeamService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.EnrollmentTeamService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService,
                            MLMConquerorGlobalEdition.Repository.Services.Teams.DualTeamService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Commissions.ICommissionsService,
                            MLMConquerorGlobalEdition.Repository.Services.Commissions.CommissionsService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Wallets.IMemberWalletService,
                            MLMConquerorGlobalEdition.Repository.Services.Wallets.MemberWalletService>();
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
builder.Services.AddScoped<MLMConquerorGlobalEdition.BizCenter.Services.Billing.ICardTokenizationService,
                            MLMConquerorGlobalEdition.BizCenter.Services.Billing.SimulatedCardTokenizationService>();
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
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", 5);
});

builder.Services.AddControllers();
builder.Services.AddHttpClient("certificates");

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

var publicKeyBase64 = builder.Configuration["Jwt:PublicKeyBase64"]
    ?? throw new InvalidOperationException("Jwt:PublicKeyBase64 not configured.");

if (publicKeyBase64.StartsWith("REPLACE_WITH_") && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "JWT RSA public key must be set before running in non-Development mode.");

RsaSecurityKey? jwtValidationKey = null;
if (!publicKeyBase64.StartsWith("REPLACE_WITH_"))
{
    var rsaValidation = RSA.Create();
    rsaValidation.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
    jwtValidationKey = new RsaSecurityKey(rsaValidation);
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = jwtValidationKey is not null,
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

// Apply pending EF migrations automatically on startup (idempotent).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseMiddleware<DomainExceptionMiddleware>();

app.UseCors("BizCenterPolicy");

app.UseSwagger();
app.UseSwaggerUI();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
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
