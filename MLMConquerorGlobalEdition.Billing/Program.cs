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
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedKernel.Behaviors;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Services.CacheService;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// ── Legacy wallet/payout gateway services (untouched) ────────────────────
builder.Services.AddScoped<StripeGatewayService>();
builder.Services.AddScoped<EWalletGatewayService>();
builder.Services.AddScoped<IGatewayService>(sp => sp.GetRequiredService<StripeGatewayService>());
builder.Services.AddScoped<IGatewayService>(sp => sp.GetRequiredService<EWalletGatewayService>());
builder.Services.AddScoped<IGatewayResolver, GatewayResolver>();

// ── Card gateway stubs ────────────────────────────────────────────────────
builder.Services.AddScoped<NmiSpreedlyGatewayService>();
builder.Services.AddScoped<NmiDirectGatewayService>();
builder.Services.AddScoped<CheckoutEurGatewayService>();
builder.Services.AddScoped<CheckoutUsGatewayService>();
builder.Services.AddScoped<CheckoutUsLlcGatewayService>();
builder.Services.AddScoped<Shift4GatewayService>();
builder.Services.AddScoped<StripeEmsGatewayService>();
builder.Services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<NmiSpreedlyGatewayService>());
builder.Services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<NmiDirectGatewayService>());
builder.Services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<CheckoutEurGatewayService>());
builder.Services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<CheckoutUsGatewayService>());
builder.Services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<CheckoutUsLlcGatewayService>());
builder.Services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<Shift4GatewayService>());
builder.Services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<StripeEmsGatewayService>());
builder.Services.AddScoped<ICardGatewayResolver, CardGatewayResolver>();

// ── Routing engine ────────────────────────────────────────────────────────
builder.Services.AddScoped<IGatewaySplitSelector, GatewaySplitSelector>();
builder.Services.AddScoped<IGatewayRouter, GatewayRouter>();
builder.Services.AddScoped<IGatewayChargeOrchestrator, GatewayChargeOrchestrator>();
builder.Services.AddSingleton<ICardBrandDetector, CardBrandDetector>();
builder.Services.AddHttpClient("CurrencyConverter");
builder.Services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();

// ── Recurring Billing Engine services ────────────────────────────────────
builder.Services.AddScoped<IRecurringBillingScheduler, RecurringBillingScheduler>();
builder.Services.AddScoped<ICommissionBalanceService, CommissionBalanceService>();
builder.Services.AddScoped<IRecurringBillingEnrollmentService, RecurringBillingEnrollmentService>();
builder.Services.AddScoped<IRecurringBillingProcessor, RecurringBillingProcessor>();

builder.Services.AddScoped<MembershipAutoRenewalJob>();
builder.Services.AddScoped<CommissionPayoutJob>();
builder.Services.AddScoped<ExchangeRateRefreshJob>();
builder.Services.AddScoped<DelayedFallbackChargeJob>();
builder.Services.AddScoped<RecurringBillingSweepJob>();

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

// Restrict this Hangfire server to its own queue so it does not pick up
// jobs whose types live in assemblies this service does not reference.
builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[] { "billing" };
});

builder.Services.AddControllers();

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

var app = builder.Build();

// Apply pending EF migrations and seed baseline gateway routing data (idempotent).
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.MigrateAsync();
    await GatewayRoutingSeeder.SeedAsync(db, logger);
}

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

app.UseHangfireDashboard("/hangfire");

RecurringJob.AddOrUpdate<MembershipAutoRenewalJob>(
    "membership-auto-renewal",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 6 * * *",                    // Daily 6:00 AM UTC
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

// Hourly exchange-rate refresh
RecurringJob.AddOrUpdate<ExchangeRateRefreshJob>(
    "exchange-rate-refresh",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 * * * *",                    // Every hour
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<RecurringBillingSweepJob>(
    "recurring-billing-sweep",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 7 * * *",                    // Daily 7:00 AM UTC (after MembershipAutoRenewalJob at 6:00 AM)
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

// Auto-payout disabled by business decision: PaymentDate on a CommissionEarning
// is purely informational (= EarnedDate + N days for "when it becomes payable")
// and must NOT trigger automatic status flips to Paid. Commission payment is
// now a manual workflow — admin triggers payouts explicitly through the
// upcoming Payouts UI. Keep the recurring schedule disabled and leave
// CommissionPayoutJob registered as a service so it can be invoked on demand
// (manually) once that flow ships.
RecurringJob.RemoveIfExists("commission-payout");
// RecurringJob.AddOrUpdate<CommissionPayoutJob>(
//     "commission-payout",
//     job => job.ExecuteAsync(CancellationToken.None),
//     "0 8 * * 5",                    // Weekly Friday 8:00 AM UTC
//     new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    var status = canConnect ? "Healthy" : "Unhealthy";
    return Results.Ok(new
    {
        service   = "MLMConquerorGlobalEdition.Billing",
        status,
        checks    = new { database = canConnect ? "Healthy" : "Unhealthy" },
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

app.Run();
