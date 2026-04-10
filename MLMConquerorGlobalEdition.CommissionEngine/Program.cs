using System.Text;
using AspNetCoreRateLimit;
using Hangfire;
using Hangfire.SqlServer;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.CommissionEngine.Jobs;
using MLMConquerorGlobalEdition.CommissionEngine.Middleware;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedKernel.Behaviors;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Services.CacheService;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// MediatR — scans all handlers in this assembly + error-handling pipeline behavior
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});


// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

// Error Tracking — singleton; uses IServiceScopeFactory for isolated DB writes
builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";
});
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

// HangFire job classes (scoped so they can inject DbContext / IMediator)
builder.Services.AddScoped<DailyResidualJob>();
builder.Services.AddScoped<BoostBonusJob>();
builder.Services.AddScoped<PresidentialBonusJob>();

// HangFire
var hangfireConn = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConn, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));
builder.Services.AddHangfireServer(options =>
    options.WorkerCount = builder.Configuration.GetValue<int>("Hangfire:WorkerCount", 5));

// Controllers
builder.Services.AddControllers();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MLMConqueror CommissionEngine API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
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

app.UseMiddleware<DomainExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// HangFire dashboard (admin-only in production — wire auth filter in Iteration 10)
var dashboardPath = builder.Configuration["Hangfire:DashboardPath"] ?? "/hangfire";
app.UseHangfireDashboard(dashboardPath);

// Register recurring jobs
RecurringJob.AddOrUpdate<DailyResidualJob>(
    "daily-residual",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 2 * * *",          // 2:00 AM UTC daily
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<BoostBonusJob>(
    "boost-bonus",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 3 * * 0",          // 3:00 AM UTC every Sunday
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<PresidentialBonusJob>(
    "presidential-bonus",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 4 1 * *",          // 4:00 AM UTC on the 1st of each month
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.Now }));

app.Run();
