using System.Text;
using AspNetCoreRateLimit;
using Hangfire;
using Hangfire.SqlServer;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Middleware;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedKernel.Behaviors;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Services.CacheService;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;

var builder = WebApplication.CreateBuilder(args);

// ── DbContext ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── MediatR — scans all handlers + error-handling pipeline behavior ───────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});

// ── Infrastructure services ───────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

// ── Error Tracking — singleton; uses IServiceScopeFactory for isolated DB writes
builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();

// ── Redis distributed cache ───────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";
});
builder.Services.AddSingleton<ICacheService, CacheService>();

// ── Firebase push notifications ───────────────────────────────────────────────
builder.Services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

// ── Payment Gateway services ──────────────────────────────────────────────────
// Register concrete gateway implementations as IGatewayService (keyed collection)
builder.Services.AddScoped<StripeGatewayService>();
builder.Services.AddScoped<EWalletGatewayService>();
builder.Services.AddScoped<IGatewayService>(sp => sp.GetRequiredService<StripeGatewayService>());
builder.Services.AddScoped<IGatewayService>(sp => sp.GetRequiredService<EWalletGatewayService>());
builder.Services.AddScoped<IGatewayResolver, GatewayResolver>();

// ── HangFire jobs ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<MembershipAutoRenewalJob>();
builder.Services.AddScoped<CommissionPayoutJob>();

// ── HangFire server ───────────────────────────────────────────────────────────
var hangfireConnStr = builder.Configuration.GetConnectionString("HangFire")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddHangfire(config =>
    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(hangfireConnStr, new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));

builder.Services.AddHangfireServer();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key is not configured.");

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

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MLMConqueror Billing API",
        Version = "v1",
        Description = "Payment processing, membership renewals, and commission payouts."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT Bearer token."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<DomainExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MLMConqueror Billing API v1");
    c.RoutePrefix = "swagger";
});
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── HangFire dashboard ────────────────────────────────────────────────────────
app.UseHangfireDashboard("/hangfire");

// ── Register recurring HangFire jobs ─────────────────────────────────────────
RecurringJob.AddOrUpdate<MembershipAutoRenewalJob>(
    "membership-auto-renewal",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 6 * * *",                    // Daily 6:00 AM UTC
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<CommissionPayoutJob>(
    "commission-payout",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 8 * * 5",                    // Weekly Friday 8:00 AM UTC
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

// ── Health check ──────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "MLMConquerorGlobalEdition.Billing",
    timestamp = DateTime.Now
}));

app.Run();
