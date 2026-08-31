using MLMConquerorGlobalEdition.SharedKernel.Server.Middleware;
using System.Text;
using AspNetCoreRateLimit;
using Hangfire;
using Hangfire.SqlServer;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.Billing.Extensions;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Middleware;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Notifications;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedKernel.Server.Behaviors;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Server.Services.CacheService;
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

// ── Card gateway — Spreedly universal proxy ───────────────────────────────
// Per BILLING-RULES §3, all processor-specific stub classes are collapsed into a
// single SpreedlyCardGatewayService. The routing engine selects the downstream
// processor; SpreedlyCardGatewayService looks up the per-processor Spreedly
// gateway token from ApiCredential and calls Spreedly's purchase endpoint.
builder.Services.AddScoped<SpreedlyCardGatewayService>();
builder.Services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<SpreedlyCardGatewayService>());
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

// ── High-volume pipeline services (§10) ──────────────────────────────────
builder.Services.AddScoped<IRecurringBillingPlanner, RecurringBillingPlanner>();
builder.Services.AddScoped<IRecurringChargeWorker, RecurringChargeWorker>();
builder.Services.AddScoped<IUplineAggregator, UplineAggregator>();
builder.Services.AddScoped<IDownstreamTriggers, DownstreamTriggers>();

// ── SharedKernel deps required by PayoutReceiptService ───────────────────
// PayoutReceiptService (registered via AddPayoutServices) depends on
// SharedKernel.Interfaces.IEmailService.  The Billing-local IDateTimeProvider
// (line ~40) satisfies the IDateTimeProvider constructor parameter because
// PayoutReceiptService lives inside the Billing.Services.* namespace hierarchy
// and the compiler resolves the unqualified name to the Billing-local type.
// IEmailService has no Billing-local equivalent, so it resolves to the
// SharedKernel interface. AddNotifications (below) registers the concrete
// implementation — SesEmailService or NullEmailService — by Notifications:Email:Provider.
// Same pattern for ISmsService / Notifications:Sms:Provider.
builder.Services.AddNotifications(builder.Configuration);

// ── Payout gateway abstraction + orchestrator ─────────────────────────────
builder.Services.AddPayoutServices();

builder.Services.AddScoped<MembershipAutoRenewalJob>();
builder.Services.AddScoped<CommissionPayoutJob>();
builder.Services.AddScoped<ExchangeRateRefreshJob>();
builder.Services.AddScoped<DelayedFallbackChargeJob>();
builder.Services.AddScoped<RecurringBillingSweepJob>();
builder.Services.AddScoped<RecurringBillingPlanningJob>();
builder.Services.AddScoped<ChargeWorkerDispatchJob>();
builder.Services.AddScoped<ChargeWorkerJob>();
builder.Services.AddScoped<UplineAggregatorJob>();
builder.Services.AddScoped<DownstreamTriggersJob>();
builder.Services.AddScoped<ReceiptAnchorJob>();
builder.Services.AddScoped<PayoutReconciliationJob>();
builder.Services.AddScoped<DeferredPayoutAccountJob>();

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

// Restrict this Hangfire server to its own queues so it does not pick up
// jobs whose types live in assemblies this service does not reference.
// Per-processor queues ("billing-{processor}") allow horizontal scaling:
// on high-volume days each processor pool can have dedicated workers.
builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[]
    {
        "billing",
        "billing-nmi-spreedly",
        "billing-nmi-direct",
        "billing-checkout-eur",
        "billing-checkout-us",
        "billing-checkout-us-llc",
        "billing-shift4",
        "billing-stripe-ems"
    };
});

builder.Services.AddControllers();

// Los tokens los firman SignupAPI y AdminAPI con RSA/RS256. Este servicio solo los
// valida, con la llave pública del mismo par — igual que AdminAPI, SignupAPI, BizCenter,
// RankEngine y TicketManagementSystem.
//
// Hasta 2026-08-28 aquí se construía una SymmetricSecurityKey (HMAC) sobre "Jwt:Key", que
// además nunca se configuró: su valor seguía siendo el placeholder del andamiaje. Una
// llave HMAC no puede verificar una firma RSA, así que todo endpoint [Authorize] de este
// servicio rechazaba cualquier token legítimo con IDX10517. La audiencia también estaba
// mal escrita ("MLMConquerorGlobalEditionClients", sin el punto).
var publicKeyBase64 = JwtKeyGuard.ValidatePublicKey(builder.Configuration["Jwt:PublicKeyBase64"]);

var rsaValidation = RSA.Create();
rsaValidation.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
var jwtValidationKey = new RsaSecurityKey(rsaValidation);

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
            IssuerSigningKey = jwtValidationKey,
            ClockSkew = TimeSpan.Zero
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

// Un token de suplantacion marcado de solo lectura no escribe. Va detras de la autorizacion para
// que un 401 o un 403 por rol se contesten antes, y delante de las rutas para que ninguna llegue a
// ejecutarse. Ver ImpersonationScope.
app.UseImpersonationReadOnly();

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

// High-volume pipeline Stage 1 — runs before RecurringBillingSweepJob.
// BatchStartTimeUtc defaults to 05:00 UTC; can be changed via GlobalParameter.
RecurringJob.AddOrUpdate<RecurringBillingPlanningJob>(
    "recurring-billing-planning",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 5 * * *",                    // Daily 5:00 AM UTC
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<RecurringBillingSweepJob>(
    "recurring-billing-sweep",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 7 * * *",                    // Daily 7:00 AM UTC — safety-net fallback after high-volume pipeline
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<ReceiptAnchorJob>(
    "receipt-anchor",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 5 * * *",                    // Daily 5:00 AM UTC
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<PayoutReconciliationJob>(
    "payout-reconciliation",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/30 * * * *",                 // Every 30 minutes — recover crash-stuck Pending payouts
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

// Abre la cuenta del proveedor para quien ya gano su primera comision y todavia no tiene una.
// Cada 15 minutos: la cuenta debe quedar lista con dias de margen antes de la corrida de
// payouts, porque i-Payout exige que el ambassador verifique su mail.
RecurringJob.AddOrUpdate<DeferredPayoutAccountJob>(
    "deferred-payout-accounts",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/15 * * * *",
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
